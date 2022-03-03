using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Numerics;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.Utilities;
using ImGuiNET;

namespace StudioCore.Gui
{
    /// <summary>
    /// A viewport is a virtual (i.e. render to texture/render target) view of a scene. It can receive input events to transform
    /// the view within a virtual canvas, or it can be manually configured for say rendering thumbnails
    /// </summary>
    public class Viewport
    {
        private bool DebugRayCastDraw = false;

        /// <summary>
        /// The camera in this scene
        /// </summary>
        public WorldView _worldView;
        private Scene.RenderScene _renderScene;
        private Scene.SceneRenderPipeline _viewPipeline;
        private MsbEditor.Selection _selection;
        private MsbEditor.ProjectSettings _settings;

        private int PrevWidth;
        private int PrevHeight;
        public int X;
        public int Y;
        public int Width;
        public int Height;

        public bool DrawGrid { get; set; } = true;

        private DebugPrimitives.DbgPrimWireGrid ViewportGrid;

        private Veldrid.Viewport _renderViewport;

        private BoundingFrustum _frustum;

        private Matrix4x4 _projectionMat;

        private DebugPrimitives.DbgPrimWire _rayDebug = null;

        //private DebugPrimitives.DbgPrimGizmoTranslate TranslateGizmo = null;
        private MsbEditor.Gizmos _gizmos;

        private MsbEditor.ActionManager _actionManager;

        private GraphicsDevice _device;

        private Scene.FullScreenQuad _clearQuad;

        private bool _vpvisible = false;

        private bool _renderTypesMenuVisible = false;

        private string _vpid = "";

        private int _cursorX = 0;
        private int _cursorY = 0;

        //public RenderTarget2D SceneRenderTarget = null;

        public Viewport(string id, GraphicsDevice device, Scene.RenderScene scene, MsbEditor.ActionManager am, MsbEditor.Selection sel, int width, int height)
        {
            _vpid = id;
            PrevWidth = width;
            PrevHeight = height;
            Width = width;
            Height = height;
            _device = device;
            float depth = device.IsDepthRangeZeroToOne ? 1 : 0;
            _renderViewport = new Veldrid.Viewport(0, 0, Width, Height, depth, 1.0f - depth);

            _renderScene = scene;
            _selection = sel;

            _worldView = new WorldView(new Veldrid.Rectangle(0, 0, Width, Height));
            _viewPipeline = new Scene.SceneRenderPipeline(scene, device, width, height);
            ViewportGrid = new DebugPrimitives.DbgPrimWireGrid(Color.Green, Color.DarkGreen, 50, 5.0f);
            //ViewportGrid.CreateDeviceObjects(device, null, ViewPipeline);

            //RenderScene.AddObject(ViewportGrid);

            _projectionMat = Utils.CreatePerspective(device, false, WorldView.FieldOfView * (float)Math.PI / 180.0f, (float)width / (float)height, WorldView.NearClip, WorldView.FarClip);
            _frustum = new BoundingFrustum(_projectionMat);
            _actionManager = am;

            _viewPipeline.SetViewportSetupAction((d, cl) =>
            {
                cl.SetFramebuffer(device.SwapchainFramebuffer);
                cl.SetViewport(0, _renderViewport);
                if (_vpvisible)
                {
                    _clearQuad.Render(d, cl);
                }
                _vpvisible = false;
            });

            _viewPipeline.SetOverlayViewportSetupAction((d, cl) =>
            {
                cl.SetFramebuffer(device.SwapchainFramebuffer);
                cl.SetViewport(0, _renderViewport);
                cl.ClearDepthStencil(0);
            });

            // Create gizmos
            _gizmos = new MsbEditor.Gizmos(_actionManager, _selection, _renderScene.OverlayRenderables);
            Scene.Renderer.AddBackgroundUploadTask((d, cl) =>
            {
                _gizmos.CreateDeviceObjects(d, cl, _viewPipeline);
            });

            _clearQuad = new Scene.FullScreenQuad();
            Scene.Renderer.AddBackgroundUploadTask((gd, cl) =>
            {
                _clearQuad.CreateDeviceObjects(gd, cl);
            });
        }

        public void LoadSettings (MsbEditor.ProjectSettings Settings)
        {
            _settings = Settings;
            _renderTypesMenuVisible = _settings.RenderTypesMenuVisible;
        }

        public Ray GetRay(float sx, float sy)
        {
            float x = (2.0f * sx) / Width - 1.0f;
            float y = 1.0f - (2.0f * sy) / Height;
            float z = 1.0f;

            Vector3 deviceCoords = new Vector3(x, y, z);

            // Clip Coordinates
            Vector4 clipCoords = new Vector4(deviceCoords.X, deviceCoords.Y, -1.0f, 1.0f);

            // View Coordinates
            Matrix4x4 invProj;
            Matrix4x4.Invert(_projectionMat, out invProj);
            Vector4 viewCoords = Vector4.Transform(clipCoords, invProj);
            viewCoords.Z = 1.0f;
            viewCoords.W = 0.0f;

            Matrix4x4 invView;
            Matrix4x4.Invert(_worldView.CameraTransform.CameraViewMatrixLH, out invView);
            Vector3 worldCoords = Vector4.Transform(viewCoords, invView).XYZ();
            worldCoords = Vector3.Normalize(worldCoords);
            //worldCoords.X = -worldCoords.X;

            return new Ray(_worldView.CameraTransform.Position, worldCoords);
        }

        public bool MouseInViewport()
        {
            var mp = InputTracker.MousePosition;
            if ((int)mp.X < X || (int)mp.X >= X + Width)
            {
                return false;
            }
            if ((int)mp.Y < Y || (int)mp.Y >= Y + Height)
            {
                return false;
            }
            return true;
        }

        public void OnGui()
        {
            if (ImGui.Begin($@"Viewport##{_vpid}", ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoNav))
            {
                if(!ImGui.IsWindowDocked())
                {
                    ImGui.Text("Viewport only works while docked to main window!");
                }

                ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding , 10);//		Round checkbox
                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing , new Vector2(3.0f, 0.0f));
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetColumnWidth() - ImGui.GetScrollX() - 8);
                bool unused = false;
                ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.0f, 0.0f, 0.0f, 1.0f));
                if (ImGui.Checkbox($@"##RenderTypes", ref unused))
                {
                    _renderTypesMenuVisible = !_renderTypesMenuVisible;
                    _settings.RenderTypesMenuVisible = _renderTypesMenuVisible;//		Update in settings so this will be saved on quit
                }
                ImGui.PopStyleColor();

                if (_renderTypesMenuVisible)
                {
                    DrawRenderTypesMenu();
                }
                ImGui.PopStyleVar(2);

                var p = ImGui.GetWindowPos();
                var s = ImGui.GetWindowSize();
                var newvp = new Veldrid.Rectangle((int)p.X, (int)p.Y + 3, (int)s.X, (int)s.Y - 3);
                ResizeViewport(_device, newvp);
                if ((InputTracker.GetMouseButtonDown(MouseButton.Right) || InputTracker.GetMouseButtonDown(MouseButton.Middle)) && MouseInViewport())
                {
                    ImGui.SetWindowFocus();
                }
                _vpvisible = true;
                var proj = Matrix4x4.Transpose(_projectionMat);
                var view = Matrix4x4.Transpose(_worldView.CameraTransform.CameraViewMatrixLH);
                var identity = Matrix4x4.Identity;
                ImGui.DrawGrid(ref view.M11, ref proj.M11, ref identity.M11, 100.0f);
            }
            ImGui.End();

            if (ImGui.Begin($@"Profiling##{_vpid}"))
            {
                ImGui.Text($@"Cull time: {_renderScene.OctreeCullTime} ms");
                ImGui.Text($@"Work creation time: {_renderScene.CPUDrawTime} ms");
                ImGui.Text($@"Scene Render CPU time: {_viewPipeline.CPURenderTime} ms");
                ImGui.Text($@"Visible objects: {_renderScene.RenderObjectCount}");
                ImGui.Text($@"Vertex Buffers Size: {Scene.Renderer.GeometryBufferAllocator.TotalVertexFootprint / 1024 / 1024} MB");
                ImGui.Text($@"Index Buffers Size: {Scene.Renderer.GeometryBufferAllocator.TotalIndexFootprint / 1024 / 1024} MB");
                ImGui.Text($@"FLVER Read Caches: {Resource.FlverResource.CacheCount}");
                ImGui.Text($@"FLVER Read Caches Size: {Resource.FlverResource.CacheFootprint / 1024 / 1024} MB");
                //ImGui.Text($@"Selected renderable:  { _viewPipeline._pickingEntity }");
            }
            PanWindow.PanWindowMiddleClick(39);
            ImGui.End();
        }

        public void SceneParamsGui()
        {
            ImGui.SliderFloat4("Light Direction", ref _viewPipeline.SceneParams.LightDirection, -1, 1);
            ImGui.SliderFloat("Direct Light Mult", ref _viewPipeline.SceneParams.DirectLightMult, 0, 3);
            ImGui.SliderFloat("Indirect Light Mult", ref _viewPipeline.SceneParams.IndirectLightMult, 0, 3);
            ImGui.SliderFloat("Brightness", ref _viewPipeline.SceneParams.SceneBrightness, 0, 5);
        }

        public void CameraParamsGui()
        {
            if(_worldView != null){	
                if (ImGui.InputFloat("Near Clip", ref WorldView.NearClip))	
                {
                    WorldView.NearClip = Math.Clamp(WorldView.NearClip, 0.0001f, WorldView.FarClip);
                }
                if (ImGui.InputFloat("Far Clip", ref WorldView.FarClip))	
                {
                    WorldView.FarClip = Math.Clamp(WorldView.FarClip, WorldView.NearClip, 100000);
                }
                if (ImGui.SliderFloat("Field of View", ref WorldView.FieldOfView, 10, 120)){}
            }
        }

        public void ResizeViewport(GraphicsDevice device, Veldrid.Rectangle newvp)
        {
            PrevWidth = Width;
            PrevHeight = Height;
            Width = newvp.Width;
            Height = newvp.Height;
            X = newvp.X;
            Y = newvp.Y;
            _worldView.UpdateBounds(newvp);
            float depth = device.IsDepthRangeZeroToOne ? 0 : 1;
            _renderViewport = new Veldrid.Viewport(newvp.X, newvp.Y, Width, Height, depth, 1.0f - depth);
        }

        private void DrawRenderTypesMenu()
        {
            ImGui.PushStyleColor(ImGuiCol.Text , new Vector4(0.0f, 0.0f, 0.0f, 1.0f));
            string label;
            uint drawFilter = (uint)_renderScene.DrawFilter;
            bool[] flags = new bool[8]
            {((drawFilter >> 0) & 0x1) > 0, ((drawFilter >> 1) & 0x1) > 0, ((drawFilter >> 2) & 0x1) > 0, ((drawFilter >> 3) & 0x1) > 0,
            ((drawFilter >> 4) & 0x1) > 0, ((drawFilter >> 5) & 0x1) > 0, ((drawFilter >> 6) & 0x1) > 0, ((drawFilter >> 7) & 0x1) > 0 };
            ImGui.SameLine();

            label = "MapPiece";
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetColumnWidth() - ImGui.GetScrollX() - ImGui.CalcTextSize(label).X - 33);
            ImGui.Text(label);
            ImGui.SameLine();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetColumnWidth() - ImGui.GetScrollX() - 30);//		Position the cursor so all checkboxes will fit
            ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.3f, 0.8f, 0.3f, 1.0f));
            if (ImGui.Checkbox($@"##MapPiece", ref flags[2]))
            {
                _renderScene.ToggleDrawFilter(Scene.RenderFilter.MapPiece);
            }
            ImGui.PopStyleColor();

            label = "Collision";
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetColumnWidth() - ImGui.GetScrollX() - ImGui.CalcTextSize(label).X - 33);
            ImGui.Text(label);
            ImGui.SameLine();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetColumnWidth() - ImGui.GetScrollX() - 30);
            ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.1f, 0.33f, 0.55f, 1.0f));
            if (ImGui.Checkbox($@"##Collision", ref flags[3]))
            {
                _renderScene.ToggleDrawFilter(Scene.RenderFilter.Collision);
            }
            ImGui.PopStyleColor();

            label = "Character";
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetColumnWidth() - ImGui.GetScrollX() - ImGui.CalcTextSize(label).X - 33);
            ImGui.Text(label);
            ImGui.SameLine();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetColumnWidth() - ImGui.GetScrollX() - 30);
            ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.8f, 0.6f, 0.8f, 1.0f));
            if (ImGui.Checkbox($@"##Character", ref flags[4]))
            {
                _renderScene.ToggleDrawFilter(Scene.RenderFilter.Character);
            }
            ImGui.PopStyleColor();

            label = "Object";
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetColumnWidth() - ImGui.GetScrollX() - ImGui.CalcTextSize(label).X - 33);
            ImGui.Text(label);
            ImGui.SameLine();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetColumnWidth() - ImGui.GetScrollX() - 30);
            ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.8f, 0.6f, 0.8f, 1.0f));
            if (ImGui.Checkbox($@"##Object", ref flags[5]))
            {
                _renderScene.ToggleDrawFilter(Scene.RenderFilter.Object);
            }
            ImGui.PopStyleColor();

            label = "Navmesh";
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetColumnWidth() - ImGui.GetScrollX() - ImGui.CalcTextSize(label).X - 33);
            ImGui.Text(label);
            ImGui.SameLine();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetColumnWidth() - ImGui.GetScrollX() - 30);
            ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.5f, 0.0f, 1.0f, 1.0f));
            if (ImGui.Checkbox($@"##Navmesh", ref flags[6]))
            {
                _renderScene.ToggleDrawFilter(Scene.RenderFilter.Navmesh);
            }
            ImGui.PopStyleColor();

            label = "Region";
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetColumnWidth() - ImGui.GetScrollX() - ImGui.CalcTextSize(label).X - 33);
            ImGui.Text(label);
            ImGui.SameLine();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetColumnWidth() - ImGui.GetScrollX() - 30);
            ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.0f, 0.0f, 1.0f, 1.0f));
            if (ImGui.Checkbox($@"##Region", ref flags[7]))
            {
                _renderScene.ToggleDrawFilter(Scene.RenderFilter.Region);
            }
            ImGui.PopStyleColor(2);
        }

        public bool Update(Sdl2Window window, float dt)
        {
            var pos = InputTracker.MousePosition;
            var ray = GetRay((float)pos.X - (float)X, (float)pos.Y - (float)Y);

            _cursorX = (int)pos.X;// - X;
            _cursorY = (int)pos.Y;// - Y;

            _gizmos.Update(ray, MouseInViewport());

            bool kbbusy = false;

            if (!_gizmos.IsMouseBusy() && MouseInViewport())
            {
                kbbusy = _worldView.UpdateInput(window, dt);
                if (InputTracker.GetMouseButtonDown(MouseButton.Left))
                {
                    _viewPipeline.CreateAsyncPickingRequest();
                }
                if (_viewPipeline.PickingResultsReady)
                {
                    var sel = _viewPipeline.GetSelection();
                    if (InputTracker.GetKey(Key.ControlLeft) || InputTracker.GetKey(Key.ControlRight) || InputTracker.GetKey(Key.ShiftLeft) || InputTracker.GetKey(Key.ShiftRight))//		Toggle selection
                    {
                        if (sel != null)
                        {
                            var action = new MsbEditor.AddSelectionAction(_selection.universe, sel);
                            _actionManager.ExecuteAction(action);
                        }
                    }
                    else
                    {
                        if (sel != null)//		Set selection
                        {
                            if (!_selection.IsSelected(sel) || _selection.GetSelection().Count > 1)//		If the object clicked on is not already the only selection
                            {
                                var action = new MsbEditor.SetSelectionAction(_selection.universe, sel);
                                _actionManager.ExecuteAction(action);
                            }
                        }
                        else if(_selection.GetSelection().Count > 0)//		Clicked on nothing. Clear selection
                        {
                            var clearAction = new MsbEditor.ClearSelectionAction(_selection.universe);
                            _actionManager.ExecuteAction(clearAction);
                        }
                    }
                }
            }

            if (InputTracker.GetKey(Key.Escape) && MouseInViewport() && _selection.GetSelection().Count > 0)
            {
                var clearAction = new MsbEditor.ClearSelectionAction(_selection.universe);
                _actionManager.ExecuteAction(clearAction);
            }

            //Gizmos.DebugGui();
            return kbbusy;
        }

        public void Draw(GraphicsDevice device, CommandList cl)
        {
            _projectionMat = Utils.CreatePerspective(device, true, WorldView.FieldOfView * (float)Math.PI / 180.0f, (float)Width / (float)Height, WorldView.NearClip, WorldView.FarClip);
            _frustum = new BoundingFrustum(_worldView.CameraTransform.CameraViewMatrixLH * _projectionMat);
            _viewPipeline.TestUpdateView(_projectionMat, _worldView.CameraTransform.CameraViewMatrixLH, _worldView.CameraTransform.Position, _cursorX, _cursorY);
            _viewPipeline.RenderScene(_frustum);

            if (_rayDebug != null)
            {
                //TODO:_debugRenderer.Add(_rayDebug, new Scene.RenderKey(0));
            }

            if (DrawGrid)
            {
                //DebugRenderer.Add(ViewportGrid, new Scene.RenderKey(0));
                //ViewportGrid.UpdatePerFrameResources(device, cl, ViewPipeline);
                //ViewportGrid.Render(device, cl, ViewPipeline);
            }

            _gizmos.CameraPosition = _worldView.CameraTransform.Position;
        }

        public void SetEnvMap(uint index)
        {
            _viewPipeline.EnvMapTexture = index;
        }

        /// <summary>
        /// Moves the camera position such that it is directly looking at the center of a
        /// bounding box. Camera will face the same direction as before.
        /// </summary>
        /// <param name="box">The bounding box to frame</param>
        public void FrameBox(BoundingBox box)
        {
            var camdir = Vector3.Transform(Vector3.UnitZ, _worldView.CameraTransform.RotationMatrix);
            var pos = box.GetCenter();
            var radius = Vector3.Distance(box.Max, box.Min);
            _worldView.CameraTransform.Position = pos - (camdir * radius);
            _worldView.OrbitCamCenter = pos;
            _worldView.OrbitCamDistance = Math.Max(radius , WorldView.NearClip);
            WorldView.FarClip = Math.Max(radius*1.2f , WorldView.FarClip);//		Don't let the camera focus something too big to see
        }
    }
}