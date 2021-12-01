using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using ImGuiNET;
using Veldrid;
using System.Windows.Forms;

namespace StudioCore.MsbEditor
{

    public struct DragDropPayload
    {
        public Entity Entity;
    }

    public struct DragDropPayloadReference
    {
        public int Index;
    }

    public interface SceneTreeEventHandler
    {
        public void OnEntityContextMenu(Entity ent);
    }

    public class SceneTree : IActionEventHandler
    {
        private Universe _universe;
        private ActionManager _editorActionManager;
        private Gui.Viewport _viewport;
        private AssetLocator _assetLocator;
        private Selection _selection;

        private string _id;

        private SceneTreeEventHandler _handler;

        private string _chaliceMapID = "m29_";
        private bool _chaliceLoadError = false;

        private bool _GCNeedsCollection = false;

        private Dictionary<string, Dictionary<MapEntity.MapEntityType, Dictionary<Type, List<MapEntity>>>> _cachedTypeView = null;

        private bool _initiatedDragDrop = false;
        private bool _pendingDragDrop = false;
        private Dictionary<int, DragDropPayload> _dragDropPayloads = new Dictionary<int, DragDropPayload>();
        private int _dragDropPayloadCounter = 0;

        private List<Entity> _dragDropSources = new List<Entity>();
        private List<int> _dragDropDests = new List<int>();
        private List<Entity> _dragDropDestObjects = new List<Entity>();

        // Keep track of open tree nodes for selection management purposes
        private HashSet<Entity> _treeOpenEntities = new HashSet<Entity>();

        private Entity _pendingClick = null;

        private bool _setNextFocus = false;

        public Dictionary<string, string> DS1NickNames = new Dictionary<string, string>(){
            {"m10_00_00_00", "Depths"},
            {"m10_01_00_00", "Undead Burg"},
            {"m10_02_00_00", "Firelink Shrine"},
            {"m11_00_00_00", "Painted World"},
            {"m12_00_00_00", "Darkroot Garden"},
            {"m12_01_00_00", "Oolacile/Abyss"},
            {"m13_00_00_00", "Catacombs"},
            {"m13_01_00_00", "Tomb of the Giants"},
            {"m13_02_00_00", "Great Hollow/Ash Lake"},
            {"m14_00_00_00", "Blighttown/Quelaag's"},
            {"m14_01_00_00", "Demon Ruins/Lost Izalith"},
            {"m15_00_00_00", "Sen's Fortress"},
            {"m15_01_00_00", "Anor Londo"},
            {"m16_00_00_00", "New Londo/Valley"},
            {"m17_00_00_00", "Duke's Archives"},
            {"m18_00_00_00", "Kiln of the First Flame"},
            {"m18_01_00_00", "Undead Asylum"},
        };
        public Dictionary<string, string> DS3NickNames = new Dictionary<string, string>(){
            {"m30_00_00_00", "High Wall of Lothric"},
            {"m30_01_00_00", "Lothric Castle"},
            {"m31_00_00_00", "Undead Settlement"},
            {"m32_00_00_00", "Archdragon Peak"},
            {"m33_00_00_00", "Road of Sacrifices/Faron"},
            {"m34_01_00_00", "Lothric Castle"},
            {"m35_00_00_00", "Cathedral of the Deep"},
            {"m37_00_00_00", "Irithyll of the Boreal Valley"},
            {"m38_00_00_00", "Smouldering  Lake"},
            {"m39_00_00_00", "Irithyll Dungeon/Profaned Capital"},
            {"m40_00_00_00", "Firelink Shrine"},
            {"m41_00_00_00", "Kiln of the First Flame"},
            {"m45_00_00_00", "Painted World of Ariandel"},
            {"m46_00_00_00", "Arena: Grand Roof"},
            {"m47_00_00_00", "Arena: Kiln of Flame"},
            {"m50_00_00_00", "Dreg Heap"},
            {"m51_00_00_00", "The Ringed City"},
            {"m51_01_00_00", "Filianore's Rest"},
            {"m53_00_00_00", "Arena: Dragon Ruins"},
            {"m54_00_00_00", "Arena: Round Plaza"},
        };

        public enum ViewMode
        {
            Hierarchy,
            Flat,
            ObjectType,
        }

        private string[] _viewModeStrings =
        {
            "Hierarchy View",
            "Flat View",
            "Type View",
        };

        private ViewMode _viewMode = ViewMode.Flat;

        public enum Configuration
        {
            MapEditor,
            ModelEditor
        }

        private Configuration _configuration;
        private ProjectSettings _settings;//		Used by Msb editor to save default ViewMode

        public SceneTree(Configuration configuration, SceneTreeEventHandler handler, string id, Universe universe, Selection sel, ActionManager aman, Gui.Viewport vp, AssetLocator al)
        {
            _handler = handler;
            _id = id;
            _universe = universe;
            _selection = sel;
            _editorActionManager = aman;
            _viewport = vp;
            _assetLocator = al;
            _configuration = configuration;
            if (_configuration == Configuration.ModelEditor)
            {
                _viewMode = ViewMode.Hierarchy;
            }
        }
        
        public void LoadSettings(ProjectSettings Settings)
        {
            _settings = Settings;
            if (_settings != null)
            {
                _viewMode = _settings.mapViewMode;
            }
        }

        private void RebuildTypeViewCache(Map map)
        {
            if (_cachedTypeView == null)
            {
                _cachedTypeView = new Dictionary<string, Dictionary<MapEntity.MapEntityType, Dictionary<Type, List<MapEntity>>>>();
            }

            var mapcache = new Dictionary<MapEntity.MapEntityType, Dictionary<Type, List<MapEntity>>>();
            mapcache.Add(MapEntity.MapEntityType.Part, new Dictionary<Type, List<MapEntity>>());
            mapcache.Add(MapEntity.MapEntityType.Region, new Dictionary<Type, List<MapEntity>>());
            mapcache.Add(MapEntity.MapEntityType.Event, new Dictionary<Type, List<MapEntity>>());
            if (_assetLocator.Type == GameType.DarkSoulsIISOTFS)
            {
                mapcache.Add(MapEntity.MapEntityType.DS2Event, new Dictionary<Type, List<MapEntity>>());
                mapcache.Add(MapEntity.MapEntityType.DS2EventLocation, new Dictionary<Type, List<MapEntity>>());
                mapcache.Add(MapEntity.MapEntityType.DS2Generator, new Dictionary<Type, List<MapEntity>>());
                mapcache.Add(MapEntity.MapEntityType.DS2GeneratorRegist, new Dictionary<Type, List<MapEntity>>());
            }

            foreach (var obj in map.Objects)
            {
                if (obj is MapEntity e && mapcache.ContainsKey(e.Type))
                {
                    var typ = e.WrappedObject.GetType();
                    if (!mapcache[e.Type].ContainsKey(typ))
                    {
                        mapcache[e.Type].Add(typ, new List<MapEntity>());
                    }
                    mapcache[e.Type][typ].Add(e);
                }
            }

            if (!_cachedTypeView.ContainsKey(map.Name))
            {
                _cachedTypeView.Add(map.Name, mapcache);
            }
            else
            {
                _cachedTypeView[map.Name] = mapcache;
            }
        }

        private void ChaliceDungeonImportButton()
        {
            ImGui.Selectable($@"   {ForkAwesome.PlusCircle} Load Chalice Dungeon...", false);
            if (ImGui.BeginPopupContextItem("chalice", 0))
            {
                ImGui.AlignTextToFramePadding();
                ImGui.Text("Chalice ID (m29_xx_xx_xx): ");
                ImGui.SameLine();
                var pname = _chaliceMapID;
                ImGui.SetNextItemWidth(100);
                if (_chaliceLoadError)
                {
                    ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.8f, 0.2f, 0.2f, 1.0f));
                }
                if (ImGui.InputText("##chalicename", ref pname, 12))
                {
                    _chaliceMapID = pname;
                }
                if (_chaliceLoadError)
                {
                    ImGui.PopStyleColor();
                }
                ImGui.SameLine();
                if (ImGui.Button("Load"))
                {
                    if (!_universe.LoadMap(_chaliceMapID))
                    {
                        _chaliceLoadError = true;
                    }
                    else
                    {
                        ImGui.CloseCurrentPopup();
                        _chaliceLoadError = false;
                        _chaliceMapID = "m29_";
                    }
                }
                ImGui.EndPopup();
            }
        }

        unsafe private void MapObjectSelectable(Entity e, bool visicon, bool hierarchial=false)
        {
            // Main selectable
            if (e is MapEntity me)
            {
                ImGui.PushID(me.Type.ToString() + e.Name);
            }
            else
            {
                ImGui.PushID(e.Name);
            }
            bool doSelect = false;
            if (_setNextFocus)
            {
                ImGui.SetItemDefaultFocus();
                _setNextFocus = false;
                doSelect = true;
            }
            bool nodeopen = false;
            string padding = hierarchial ? "   " : "    ";
            if (hierarchial && e.Children.Count > 0)
            {
                var treeflags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanAvailWidth;
                if ( _selection.GetSelection().Contains(e))
                {
                    treeflags |= ImGuiTreeNodeFlags.Selected;
                }
                nodeopen = ImGui.TreeNodeEx(e.PrettyName, treeflags);
                if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(0))
                {
                    if (e.RenderSceneMesh != null)
                    {
                        _viewport.FrameBox(e.RenderSceneMesh.GetBounds());
                    }
                }
            }
            else
            {
                if (ImGui.Selectable(padding + e.PrettyName, _selection.GetSelection().Contains(e), ImGuiSelectableFlags.AllowDoubleClick | ImGuiSelectableFlags.AllowItemOverlap))
                {
                    // If double clicked frame the selection in the viewport
                    if (ImGui.IsMouseDoubleClicked(0))
                    {
                        if (e.RenderSceneMesh != null)
                        {
                            _viewport.FrameBox(e.RenderSceneMesh.GetBounds());
                        }
                    }
                }
            }
            if (ImGui.IsItemClicked(0))
            {
                _pendingClick = e;
            }

            if (_pendingClick == e && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
            {
                if (ImGui.IsItemHovered())
                {
                    doSelect = true;
                }
                _pendingClick = null;
            }

            if (ImGui.IsItemFocused() && !_selection.IsSelected(e))
            {
                doSelect = true;
            }

            if (hierarchial && doSelect)
            {
                if ((nodeopen && !_treeOpenEntities.Contains(e)) ||
                    (!nodeopen && _treeOpenEntities.Contains(e)))
                {
                    doSelect = false;
                }

                if (nodeopen && !_treeOpenEntities.Contains(e))
                {
                    _treeOpenEntities.Add(e);
                }
                else if (!nodeopen && _treeOpenEntities.Contains(e))
                {
                    _treeOpenEntities.Remove(e);
                }
            }

            if (ImGui.BeginPopupContextItem())
            {
                _handler.OnEntityContextMenu(e);
                ImGui.EndPopup();
            }

            if (ImGui.BeginDragDropSource())
            {
                ImGui.Text(e.PrettyName);
                // Kinda meme
                DragDropPayload p = new DragDropPayload();
                p.Entity = e;
                _dragDropPayloads.Add(_dragDropPayloadCounter, p);
                DragDropPayloadReference r = new DragDropPayloadReference();
                r.Index = _dragDropPayloadCounter;
                _dragDropPayloadCounter++;
                GCHandle handle = GCHandle.Alloc(r, GCHandleType.Pinned);
                ImGui.SetDragDropPayload("entity", handle.AddrOfPinnedObject(), (uint)sizeof(DragDropPayloadReference));
                ImGui.EndDragDropSource();
                handle.Free();
                _initiatedDragDrop = true;
            }
            if (hierarchial && ImGui.BeginDragDropTarget())
            {
                var payload = ImGui.AcceptDragDropPayload("entity");
                if (payload.NativePtr != null)
                {
                    DragDropPayloadReference* h = (DragDropPayloadReference*)payload.Data;
                    var pload = _dragDropPayloads[h->Index];
                    _dragDropPayloads.Remove(h->Index);
                    _dragDropSources.Add(pload.Entity);
                    _dragDropDestObjects.Add(e);
                    _dragDropDests.Add(e.Children.Count);
                }
                ImGui.EndDragDropTarget();
            }

            // Visibility icon
            if (visicon)
            {
                ImGui.SetItemAllowOverlap();
                bool visible = e.EditorVisible;
                ImGui.SameLine(ImGui.GetWindowContentRegionWidth() - 18.0f);
                ImGui.PushStyleColor(ImGuiCol.Text, visible ? new Vector4(1.0f, 1.0f, 1.0f, 1.0f)
                    : new Vector4(0.6f, 0.6f, 0.6f, 1.0f));
                ImGui.TextWrapped(visible ? ForkAwesome.Eye : ForkAwesome.EyeSlash);
                ImGui.PopStyleColor();
                if (ImGui.IsItemClicked(0))
                {
                    e.EditorVisible = !e.EditorVisible;
                    doSelect = false;
                }
            }

            // If the visibility icon wasn't clicked actually perform the selection
            if (doSelect)
            {
                if (InputTracker.GetKey(Key.ControlLeft) || InputTracker.GetKey(Key.ControlRight))
                {
                    _selection.AddSelection(e);
                }
                else
                {
                    _selection.ClearSelection();
                    _selection.AddSelection(e);
                }
            }

            ImGui.PopID();

            // Invisible item to be a drag drop target between nodes
            if (_pendingDragDrop)
            {
                if (e is MapEntity me2)
                {
                    ImGui.SetItemAllowOverlap();
                    ImGui.InvisibleButton(me2.Type.ToString() + e.Name, new Vector2(-1, 3.0f));
                }
                else
                {
                    ImGui.SetItemAllowOverlap();
                    ImGui.InvisibleButton(e.Name, new Vector2(-1, 3.0f));
                }
                if (ImGui.IsItemFocused())
                {
                    _setNextFocus = true;
                }
                if (ImGui.BeginDragDropTarget())
                {
                    var payload = ImGui.AcceptDragDropPayload("entity");
                    if (payload.NativePtr != null)
                    {
                        DragDropPayloadReference* h = (DragDropPayloadReference*)payload.Data;
                        var pload = _dragDropPayloads[h->Index];
                        _dragDropPayloads.Remove(h->Index);
                        if (hierarchial)
                        {
                            _dragDropSources.Add(pload.Entity);
                            _dragDropDestObjects.Add(e.Parent);
                            _dragDropDests.Add(e.Parent.ChildIndex(e) + 1);
                        }
                        else
                        {
                            _dragDropSources.Add(pload.Entity);
                            _dragDropDests.Add(pload.Entity.Container.Objects.IndexOf(e) + 1);
                        }

                    }
                    ImGui.EndDragDropTarget();
                }
            }

            // If there's children then draw them
            if (nodeopen)
            {
                HierarchyView(e);
                ImGui.TreePop();
            }
        }

        private void HierarchyView(Entity entity)
        {
            foreach (var obj in entity.Children)
            {
                if (obj is Entity e)
                {
                    MapObjectSelectable(e, true, true);
                }
            }
        }

        private void FlatView(Map map)
        {
            foreach (var obj in map.Objects)
            {
                if (obj is MapEntity e)
                {
                    MapObjectSelectable(e, true);
                }
            }
        }

        private void TypeView(Map map)
        {
            if (_cachedTypeView == null || !_cachedTypeView.ContainsKey(map.Name))
            {
                RebuildTypeViewCache(map);
            }

            foreach (var cats in _cachedTypeView[map.Name].OrderBy(q => q.Key.ToString()))
            {
                if (cats.Value.Count > 0)
                {
                    if (ImGui.TreeNodeEx(cats.Key.ToString(), ImGuiTreeNodeFlags.OpenOnArrow))
                    {
                        foreach (var typ in cats.Value.OrderBy(q => q.Key.Name))
                        {
                            if (typ.Value.Count > 0)
                            {
                                // Regions don't have multiple types in games before DS3
                                if (cats.Key == MapEntity.MapEntityType.Region &&
                                    _assetLocator.Type != GameType.DarkSoulsIII && _assetLocator.Type != GameType.Sekiro)
                                {
                                    foreach (var obj in typ.Value)
                                    {
                                        MapObjectSelectable(obj, true);
                                    }
                                }
                                else if (ImGui.TreeNodeEx(typ.Key.Name, ImGuiTreeNodeFlags.OpenOnArrow))
                                {
                                    foreach (var obj in typ.Value)
                                    {
                                        MapObjectSelectable(obj, true);
                                    }
                                    ImGui.TreePop();
                                }
                            }
                            else
                            {
                                ImGui.Text($@"   {typ.Key.ToString()}");
                            }
                        }
                        ImGui.TreePop();
                    }
                }
                else
                {
                    ImGui.Text($@"   {cats.Key.ToString()}");
                }

            }
        }

        public void OnGui()
        {
            ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.145f, 0.145f, 0.149f, 1.0f));
            if (_configuration == Configuration.MapEditor)
            {
                ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0.0f, 0.0f));
            }
            else
            {
                ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0.0f, 2.0f));
            }
            string titleString = _configuration == Configuration.MapEditor ? $@"Map Object List##{_id}" : $@"Model Hierarchy##{_id}";
            if (ImGui.Begin(titleString))
            {
                if (_initiatedDragDrop)
                {
                    _initiatedDragDrop = false;
                    _pendingDragDrop = true;
                }
                if (_pendingDragDrop && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                {
                    _pendingDragDrop = false;
                }

                ImGui.PopStyleVar();
                ImGui.SetNextItemWidth(-1);
                if (_configuration == Configuration.MapEditor)
                {
                    int mode = (int)_viewMode;
                    if (ImGui.Combo("##typecombo", ref mode, _viewModeStrings, _viewModeStrings.Length))
                    {
                        _viewMode = (ViewMode)mode;
                        if(_settings != null)//		Update default view mode
                        {
                            _settings.mapViewMode = _viewMode;
                        }
                    }
                }

                ImGui.BeginChild("listtree");
                Map pendingUnload = null;
                foreach (var lm in _universe.LoadedObjectContainers.OrderBy((k) => k.Key))
                {
                    var map = lm.Value;
                    var mapid = lm.Key;
                    string mapName = GetMapNickName(mapid);

                    var treeflags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanAvailWidth;
                    if (map != null && _selection.GetSelection().Contains(map.RootObject))
                    {
                        treeflags |= ImGuiTreeNodeFlags.Selected;
                    }
                    bool nodeopen = false;
                    string unsaved = (map != null && map.HasUnsavedChanges) ? "*" : "";

                    if (map != null)//		Create map (Parent) objects
                    {
                        nodeopen = ImGui.TreeNodeEx($@"{ForkAwesome.Cube} {mapid}", treeflags, $@"{ForkAwesome.Cube} {mapid}{unsaved}");
                    }
                    else//		Create map Load buttons
                    {
                        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(3,3));
                        if(ImGui.Button($@" {ForkAwesome.Cube} {mapName}"))
                        {
                            _universe.LoadMap(mapid);
                        }
                        ImGui.PopStyleVar();
                    }

                    // Right click context menu
                    if (ImGui.BeginPopupContextItem($@"mapcontext_{mapid}"))
                    {
                        if (map == null)
                        {
                            if (ImGui.Selectable("Load Map"))
                            {
                                _universe.LoadMap(mapid);
                            }
                        }
                        else if (map is Map m)
                        {
                            if (ImGui.Selectable("Save Map"))
                            {
                                try
                                {
                                    _universe.SaveMap(m);
                                }
                                catch (SavingFailedException e)
                                {
                                    System.Windows.Forms.MessageBox.Show(e.Wrapped.Message, e.Message,
                                         System.Windows.Forms.MessageBoxButtons.OK,
                                         System.Windows.Forms.MessageBoxIcon.None);
                                }
                            }
                            if (ImGui.Selectable("Unload Map"))
                            {
                                _selection.ClearSelection();
                                _editorActionManager.Clear();
                                pendingUnload = m;
                            }
                        }
                        ImGui.EndPopup();
                    }
                    
                    //		Unload/Save Buttons
                    if (map != null)//		Create elements in map
                    {
                        ImGui.SameLine();
                        ImGui.BeginChild(mapName, new Vector2(120, 16));
                        if (ImGui.Button("Unload"))
                        {
                            _selection.ClearSelection();
                            _editorActionManager.Clear();
                            pendingUnload = map as Map;
                        }
                        ImGui.SameLine();
                        if (ImGui.Button("Save"))
                    //	if (ImGui.Button("Save: " + mapName))
                        {
                            try
                            {
                                _universe.SaveMap(map as Map);
                            }
                            catch (SavingFailedException e)
                            {
                                System.Windows.Forms.MessageBox.Show(e.Wrapped.Message, e.Message,
                                        System.Windows.Forms.MessageBoxButtons.OK,
                                        System.Windows.Forms.MessageBoxIcon.None);
                            }
                        }
                        ImGui.EndChild();
                    }

                    if (ImGui.IsItemClicked() && map != null)
                    {
                        _pendingClick = map.RootObject;
                    }
                    if (map != null && _pendingClick == map.RootObject && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                    {
                        if (ImGui.IsItemHovered())
                        {
                            // Only select if a node is not currently being opened/closed
                            if ((nodeopen && _treeOpenEntities.Contains(map.RootObject)) ||
                                (!nodeopen && !_treeOpenEntities.Contains(map.RootObject)))
                            {
                                if (InputTracker.GetKey(Key.ShiftLeft) || InputTracker.GetKey(Key.ShiftRight))
                                {
                                    _selection.AddSelection(map.RootObject);
                                }
                                else
                                {
                                    _selection.ClearSelection();
                                    _selection.AddSelection(map.RootObject);
                                }
                            }

                            // Update the open/closed state
                            if (nodeopen && !_treeOpenEntities.Contains(map.RootObject))
                            {
                                _treeOpenEntities.Add(map.RootObject);
                            }
                            else if (!nodeopen && _treeOpenEntities.Contains(map.RootObject))
                            {
                                _treeOpenEntities.Remove(map.RootObject);
                            }
                        }
                        _pendingClick = null;
                    }
                    if (nodeopen)
                    {
                        if (_pendingDragDrop)
                        {
                            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8.0f, 0.0f));
                        }
                        else
                        {
                            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8.0f, 3.0f));
                        }
                        if (_viewMode == ViewMode.Hierarchy)
                        {
                            HierarchyView(map.RootObject);
                        }
                        else if (_viewMode == ViewMode.Flat)
                        {
                            FlatView((Map)map);
                        }
                        else if (_viewMode == ViewMode.ObjectType)
                        {
                            TypeView((Map)map);
                        }
                        ImGui.PopStyleVar();
                        ImGui.TreePop();
                    }
                }
                if (_assetLocator.Type == GameType.Bloodborne && _configuration == Configuration.MapEditor)
                {
                    ChaliceDungeonImportButton();
                }
                ImGui.EndChild();

                if (_dragDropSources.Count > 0)
                {
                    if (_dragDropDestObjects.Count > 0)
                    {
                        var action = new ChangeEntityHierarchyAction(_universe, _dragDropSources, _dragDropDestObjects, _dragDropDests, false);
                        _editorActionManager.ExecuteAction(action);
                        _dragDropSources.Clear();
                        _dragDropDests.Clear();
                        _dragDropDestObjects.Clear();
                    }
                    else
                    {
                        var action = new ReorderContainerObjectsAction(_universe, _dragDropSources, _dragDropDests, false);
                        _editorActionManager.ExecuteAction(action);
                        _dragDropSources.Clear();
                        _dragDropDests.Clear();
                    }
                }

                if (pendingUnload != null)
                {
                    _universe.UnloadMap(pendingUnload);
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    _GCNeedsCollection = true;
                    Resource.ResourceManager.UnloadUnusedResources();
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                }
            }
            else
            {
                ImGui.PopStyleVar();
            }
            ImGui.End();
            ImGui.PopStyleColor();
        }

        string GetMapNickName(string mapid){
            switch(_assetLocator.Type)
            {
                case GameType.DemonsSouls:
                    break;
                case GameType.DarkSoulsPTDE:
                case GameType.DarkSoulsRemastered:
                    if(DS1NickNames.ContainsKey(mapid))
                        return mapid + " - " + DS1NickNames[mapid];
                    break;
                case GameType.DarkSoulsIISOTFS:
                    return mapid;
                case GameType.DarkSoulsIII:
                    if(DS3NickNames.ContainsKey(mapid))
                        return mapid + " - " + DS3NickNames[mapid];
                    break;
                case GameType.Bloodborne:
                    break;
                case GameType.Sekiro:
                    break;
            }
            return mapid;
        }

        public void OnActionEvent(ActionEvent evt)
        {
            if (evt.HasFlag(ActionEvent.ObjectAddedRemoved))
            {
                _cachedTypeView = null;
            }
        }
    }
}
