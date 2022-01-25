using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;

namespace StudioCore.MsbEditor
{
    public class DisplayGroupsEditor
    {
        private ActionManager _editorActionManager;
        private Scene.RenderScene _scene;
        private Selection _selection;
        
        bool groupsHaveChanged = false;
        uint[][] preActionDrawgroups;
        uint[][] preActionDispgroups;

        bool mouseHeldToSetLayer;
        bool setChecked;

        Vector2 cursorWindowPos;
        Vector2 firstCheckboxWindowPos = new Vector2(157 , 62);

        //		I arbitrarily chose for there to be 4 slots. Any number should work.
        uint[][] copiedDrawGroups = new uint[4][]{null , null , null , null};
        uint[][] copiedDispGroups = new uint[4][]{null , null , null , null};
        string[] copiedDrawDisplay = new string[4];
        string[] copiedDispDisplay = new string[4];
        string[] groupNames = new string[4]{"A", "B", "C", "D"};//		Buttons have to have unique names in imGui.

        public DisplayGroupsEditor(ActionManager editorActionManager, Scene.RenderScene scene, Selection sel)
        {
            _editorActionManager = editorActionManager;
            _scene = scene;
            _selection = sel;
        }

        public void Clear()
        {
            copiedDrawGroups = new uint[4][]{null , null , null , null};
            copiedDispGroups = new uint[4][]{null , null , null , null};
            copiedDrawDisplay = new string[4];
            copiedDispDisplay = new string[4];
            groupsHaveChanged = false;
        }

        public void OnGui(GameType game)//		This runs every frame and the ImGui.Button is a promt to check if the button has been pressed
        {
            var layerCount = (game == GameType.DemonsSouls || game == GameType.DarkSoulsPTDE || game == GameType.DarkSoulsRemastered || game == GameType.DarkSoulsIISOTFS) ? 4 : 8;

            uint[] sdrawgroups = null;
            uint[] sdispgroups = null;
            List<Entity> groupSelection = new List<Entity>();
            HashSet<Entity> uncheckedGroupSelection = _selection.GetFilteredSelection<Entity>();

            foreach (Entity ent in uncheckedGroupSelection){
                if(ent.UseDrawGroups)
                    groupSelection.Add(ent);
            }

            if (groupSelection.Count != 0)
            {
                sdrawgroups = new uint[layerCount];
                sdispgroups = new uint[layerCount];
                for (int i = 0; i < layerCount; i++)//		Set every group to fully on
                {
                    sdrawgroups[i] = uint.MaxValue;
                    sdispgroups[i] = uint.MaxValue;
                }
                foreach (Entity ent in groupSelection)//		Bitwise AND the amassed layer data with each entity to remove every layer not used by at least one selected entity
                {
                    for (int i = 0; i < layerCount; i++)//		For every group
                    {
                        sdrawgroups[i] = sdrawgroups[i] & ent.Drawgroups[i];
                        sdispgroups[i] = sdispgroups[i] & ent.Dispgroups[i];
                    }
                }
            }

            if (InputTracker.GetMouseButtonDown(Veldrid.MouseButton.Left)){//		New action started. Record initial layer data for Undo.
                groupsHaveChanged = false;
                preActionDrawgroups = new uint[groupSelection.Count][];
                preActionDispgroups = new uint[groupSelection.Count][];
                for (int p = 0 ; p < groupSelection.Count ; p++){//		Record all object's groups in order
                    preActionDrawgroups[p] = new uint[layerCount];
                    preActionDispgroups[p] = new uint[layerCount];
                    groupSelection[p].Drawgroups.CopyTo(preActionDrawgroups[p] , 0);
                    groupSelection[p].Dispgroups.CopyTo(preActionDispgroups[p] , 0);
                }
            }

            ImGui.SetNextWindowSize(new Vector2(100, 100));
            if (ImGui.Begin("Display Groups"))
            {
                var dg = _scene.DisplayGroup;
                if (dg.AlwaysVisible || dg.Drawgroups.Length != layerCount)
                {
                    dg.Drawgroups = new uint[layerCount];
                    for (int i = 0; i < layerCount; i++)
                    {
                        dg.Drawgroups[i] = 0xFFFFFFFF;
                    }
                    dg.AlwaysVisible = false;
                }

                ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.45f, 0.45f, 0.05f, 1.0f));
                if (ImGui.Button("Show All"))
                {
                    for (int i = 0; i < layerCount; i++)
                    {
                        dg.Drawgroups[i] = 0xFFFFFFFF;
                    }
                }
                ImGui.SameLine();
                if (ImGui.Button("Hide All"))
                {
                    for (int i = 0; i < layerCount; i++)
                    {
                        dg.Drawgroups[i] = 0;
                    }
                }
                ImGui.SameLine();
                if (sdispgroups != null && ImGui.Button("Set from Selected"))
                {
                    bool noActiveGroups = true;
                    for (int i = 0; i < layerCount; i++)
                    {
                        dg.Drawgroups[i] = sdispgroups[i];
                        if(sdispgroups[i] > 0)//		At least one layer from this group is on
                            noActiveGroups = false;
                    }
                    //		No Disp groups were enabled on the selected object. Hiding everything was likely not the intention. Set from Draw Groups.
                    if(noActiveGroups && sdrawgroups != null)
                    {
                        for (int i = 0; i < layerCount; i++)
                        {
                            dg.Drawgroups[i] = sdrawgroups[i];
                        }
                    }
                }
                ImGui.PopStyleColor();

                ImGui.SameLine();
                ImGui.Text("Red: Draw Group. Green: Disp Group.");
                ImGui.SameLine();
                if (ImGui.Button("?"))//		Invert group button (Buttons have to have unique names)
                {
                    System.Windows.Forms.MessageBox.Show(
                        "Click on a checkbox to toggle that layer's visibility.\n" + 
                        "Click and drag to set all boxes passed over to the same value.\n" + 
                        "Hold SHIFT to change selected object's draw groups.\n" + 
                        "Hold CONTROL to change selected object's display groups.\n" + 
                        "Editing multiple object's Group data will only ever change the bite you clicked/drug over. (They will not inherit the groups shown)\n" +
                        "Only layers enabled on every object selected will be colored."
                        ,
                        "How To Use"
                    );
                }
                cursorWindowPos = InputTracker.MousePosition - ImGui.GetWindowPos();

                
                //		Mouse released, stop dragging layer settings
                if(mouseHeldToSetLayer && !InputTracker.GetMouseButton(Veldrid.MouseButton.Left))
                {
                    mouseHeldToSetLayer = false;
                    if(groupsHaveChanged){//		Record the change as an action on the Undo stack. 
                        uint[][] newDrawgroups = new uint[groupSelection.Count][];
                        uint[][] newDispgroups = new uint[groupSelection.Count][];
                        for (int p = 0 ; p < groupSelection.Count ; p++){//		Record all object's groups in order
                            newDrawgroups[p] = new uint[layerCount];
                            newDispgroups[p] = new uint[layerCount];
                            groupSelection[p].Drawgroups.CopyTo(newDrawgroups[p] , 0);
                            groupSelection[p].Dispgroups.CopyTo(newDispgroups[p] , 0);
                        }

                        var action = new ChangeDisplayGroups(groupSelection, newDrawgroups , preActionDrawgroups, newDispgroups, preActionDispgroups);
                        _editorActionManager.ExecuteAction(action);
                    }
                }

                for (int g = 0; g < dg.Drawgroups.Length; g++)//		For every group
                {
                    if (ImGui.Button($@"Display Group {g}: "))//		Invert group button (Buttons have to have unique names)
                    {
                        uint highBits = 0;
                        for (int i = 0; i < 32; i++)//		Count how many bits are high
                        {
                            highBits += ((dg.Drawgroups[g] >> i) & 0x1);
                        }

                        if(highBits >= 16)//		There were more high bits than low (or equal), set all low
                            dg.Drawgroups[g] = 0x00000000;
                        else
                            dg.Drawgroups[g] = 0xFFFFFFFF;
                    }

                    for (int i = 0; i < 32; i++)//		For every column
                    {
                        bool check = ((dg.Drawgroups[g] >> i) & 0x1) > 0;//		Group is enabled
                        Vector2 cursorRelativeToCheckbox = cursorWindowPos - (firstCheckboxWindowPos + new Vector2(i*28 , g*25));

                        bool cursorOverCheckbox = MathF.Abs(cursorRelativeToCheckbox.X) < 14 &&  MathF.Abs(cursorRelativeToCheckbox.Y) < 13;//		Leaves a 1 pixel gap between checkboxes on the X and 0 on the y

                    /*	//		A slightly more/less jank way to find cursor position relative to an object is:
                        ImGui.SameLine();
                        Vector2 buttonStartPos = ImGui.GetCursorPos();
                        ImGui.Checkbox($@"##dispgroup{g}{i}", ref check);
                        ImGui.SameLine();
                        Vector2 buttonEndPos = ImGui.GetCursorPos();
                        ImGui.NewLine();
                        if(cursorWindowPos.X > buttonStartPos.X && 
                            cursorWindowPos.X < ImGui.GetCursorPos().X && 
                            cursorWindowPos.Y > buttonStartPos.Y && 
                            cursorWindowPos.Y < buttonStartPos.Y+6){}//		Mouse is over the checkbox
                    */



                        ImGui.SameLine();
                        bool red = sdrawgroups != null && (((sdrawgroups[g] >> i) & 0x1) > 0);//		Color code based on the selected object
                        bool green = sdispgroups != null && (((sdispgroups[g] >> i) & 0x1) > 0);

                        if (red && green)//		When both are selected, use both colors
                        {
                            ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.4f, 0.3f, 0.1f, 1.0f));
                            ImGui.PushStyleColor(ImGuiCol.CheckMark, new Vector4(0.2f, 1.0f, 0.2f, check?1.0f:0.2f));
                        }
                        else if (red)
                        {
                            ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.4f, 0.06f, 0.06f, 1.0f));
                            ImGui.PushStyleColor(ImGuiCol.CheckMark, new Vector4(1.0f, 0.2f, 0.2f, check?1.0f:0.2f));
                        }
                        else if (green)
                        {
                            ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.02f, 0.3f, 0.02f, 1.0f));
                            ImGui.PushStyleColor(ImGuiCol.CheckMark, new Vector4(0.2f, 1.0f, 0.2f, check?1.0f:0.2f));
                        }

                        ImGui.Checkbox($@"##dispgroup{g}{i}", ref check);//		Don't use the check box as a function to check for input. It only works on mouse up.
                        if (cursorOverCheckbox)
                        {	
                            if (InputTracker.GetMouseButtonDown(Veldrid.MouseButton.Left))
                            {
                                bool toggleCheckMark = true;
                                if (sdrawgroups != null && InputTracker.GetKey(Veldrid.Key.LShift) || InputTracker.GetKey(Veldrid.Key.RShift))//		Shift held. Toggle selected object's Draw Group.
                                {
                                    uint changeThisBit = (uint)Math.Round(MathF.Pow(2 , i));
                                    setChecked = (changeThisBit & sdrawgroups[g]) <= 0;
                                    toggleCheckMark = SetDrawLayer(g, changeThisBit, setChecked, groupSelection);
                                    mouseHeldToSetLayer = true;

                                }
                                if (sdispgroups != null && InputTracker.GetKey(Veldrid.Key.LControl) || InputTracker.GetKey(Veldrid.Key.RControl))//		Control held. Toggle selected object's Disp Group.
                                {
                                    uint changeThisBit = (uint)Math.Round(MathF.Pow(2 , i));
                                    setChecked = (changeThisBit & sdispgroups[g]) <= 0;
                                    toggleCheckMark = SetDispLayer(g, changeThisBit, setChecked, groupSelection);
                                    mouseHeldToSetLayer = true;
                                
                                }
                                
                                if (toggleCheckMark)//		Normal layer toggling
                                {
                                    check = !check;
                                    mouseHeldToSetLayer = true;
                                    setChecked = check;
                                    if (check)//		Check has changed. Invert the effected bit.
                                    {
                                        dg.Drawgroups[g] |= (1u << i);
                                    }
                                    else
                                    {
                                        dg.Drawgroups[g] &= ~(1u << i);
                                    }
                                }
                            }
                            else if (mouseHeldToSetLayer && InputTracker.GetMouseButton(Veldrid.MouseButton.Left))//		Dragging the mouse across checkboxes
                            {
                                bool toggleCheckMark = true;
                                if(sdrawgroups != null && InputTracker.GetKey(Veldrid.Key.LShift) || InputTracker.GetKey(Veldrid.Key.RShift))//		Shift held. Toggle selected object's Draw Group.
                                {
                                    toggleCheckMark = false;
                                    uint changeThisBit = (uint)Math.Round(MathF.Pow(2 , i));
                                    SetDrawLayer(g, changeThisBit, setChecked, groupSelection);
                                }

                                if(sdispgroups != null && InputTracker.GetKey(Veldrid.Key.LControl) || InputTracker.GetKey(Veldrid.Key.RControl))
                                {
                                    toggleCheckMark = false;
                                    uint changeThisBit = (uint)Math.Round(MathF.Pow(2 , i));
                                    SetDispLayer(g, changeThisBit, setChecked, groupSelection);
                                }

                                if (toggleCheckMark)
                                {
                                    if (setChecked)//		Check has changed. Invert the effected bit.
                                    {
                                        dg.Drawgroups[g] |= (1u << i);
                                    }
                                    else
                                    {
                                        dg.Drawgroups[g] &= ~(1u << i);
                                    }
                                }

                            }
                        }
                        if (red || green)
                        {
                            ImGui.PopStyleColor(2);
                        }
                    }
                }
            }
            
            if(sdispgroups != null)//		Something is selected
            {
                ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.45f, 0.45f, 0.05f, 1.0f));
                for (int saveSlot = 0; saveSlot < 4; saveSlot++)//		4 Copy Paste Buttons
                {
                    if (ImGui.Button("Copy object(s) " + groupNames[saveSlot]))//	Save groups
                    {
                        copiedDrawGroups[saveSlot] = new uint[sdrawgroups.Length];
                        copiedDispGroups[saveSlot] = new uint[sdispgroups.Length];
                        Array.Copy(sdrawgroups , copiedDrawGroups[saveSlot] , sdrawgroups.Length);
                        Array.Copy(sdispgroups , copiedDispGroups[saveSlot] , sdispgroups.Length);
                        copiedDrawDisplay[saveSlot] = "Draw:";
                        copiedDispDisplay[saveSlot] = "Disp:";
                        for (int g = 0; g < sdrawgroups.Length; g++)//		Record the hex display of the saved layers
                        {
                            copiedDrawDisplay[saveSlot] += copiedDrawGroups[saveSlot][g].ToString("x8") + ",";
                            copiedDispDisplay[saveSlot] += copiedDispGroups[saveSlot][g].ToString("x8") + ",";
                        }
                    }
                    if (copiedDrawGroups[saveSlot] != null)
                    {
                        ImGui.SameLine();
                        ImGui.Text(copiedDrawDisplay[saveSlot]);
                        ImGui.SameLine();
                        if (ImGui.Button("Paste " + groupNames[saveSlot] + " to object(s)"))//		Groups have been copied
                        {
                            preActionDrawgroups = new uint[groupSelection.Count][];
                            preActionDispgroups = new uint[groupSelection.Count][];
                            uint[][] newDrawgroups = new uint[groupSelection.Count][];
                            uint[][] newDispgroups = new uint[groupSelection.Count][];
                            for (int i = 0 ; i < groupSelection.Count ; i++){
                                preActionDrawgroups[i] = new uint[layerCount];
                                preActionDispgroups[i] = new uint[layerCount];
                                newDrawgroups[i] = new uint[layerCount];
                                newDispgroups[i] = new uint[layerCount];
                                groupSelection[i].Drawgroups.CopyTo(preActionDrawgroups[i] , 0);
                                groupSelection[i].Dispgroups.CopyTo(preActionDispgroups[i] , 0);
                                copiedDrawGroups[saveSlot].CopyTo(newDrawgroups[i] , 0);
                                copiedDispGroups[saveSlot].CopyTo(newDispgroups[i] , 0);
                            }

                            var action = new ChangeDisplayGroups(groupSelection, newDrawgroups , preActionDrawgroups, newDispgroups, preActionDispgroups);
                            _editorActionManager.ExecuteAction(action);

                        }
                        ImGui.SameLine();
                        ImGui.Text(copiedDispDisplay[saveSlot]);
                    }
                }
                ImGui.PopStyleColor();
            }
            PanWindow.PanWindowMiddleClick(20);
            ImGui.End();
        }

        bool SetDrawLayer(int group, uint changeThisBit, bool setBitHigh, List<Entity> groupSelection)
        {
            if (groupSelection.Count > 0)
            {
                groupsHaveChanged = true;
                foreach (Entity ent in groupSelection)
                {
                    if (ent.UseDrawGroups){
                        if (setBitHigh)//									Set bit high reguardless of its current state by ORing it with changeThisBit
                            ent.Drawgroups[group] = ent.Drawgroups[group] | changeThisBit;
                        else//		Set bit low reguardless of its current state by ANDing it with the inverse of changeThisBit
                            ent.Drawgroups[group] = ent.Drawgroups[group] & ~changeThisBit;
                    }
                }
                return false;
            }
            return true;//		Return true, telling the program to invert the check box since nothing was changed here.
        }


        bool SetDispLayer(int group, uint changeThisBit, bool setBitHigh, List<Entity> groupSelection)
        {
            if (groupSelection.Count > 0)
            {
                groupsHaveChanged = true;
                foreach (Entity ent in groupSelection)
                {
                    if (ent.UseDrawGroups && ent.UseDrawGroups){
                        if (setBitHigh)//									Set bit high reguardless of its current state by ORing it with changeThisBit
                            ent.Dispgroups[group] = ent.Dispgroups[group] | changeThisBit;
                        else//		Set bit low reguardless of its current state by ANDing it with the inverse of changeThisBit
                            ent.Dispgroups[group] = ent.Dispgroups[group] & ~changeThisBit;
                    }
                }
                return false;
            }
            return true;
        }
    }
}