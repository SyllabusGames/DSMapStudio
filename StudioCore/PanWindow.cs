using System.Collections.Generic;
using System.Numerics;
using Veldrid.Sdl2;
using ImGuiNET;

namespace StudioCore
{
    public class PanWindow
    {

        public static Sdl2Window Window;//		This is set in MsbEditorScreen() / ModelEditorScreen()
        
        private static Dictionary<int, bool> _mouseCurrentlyPanning = new Dictionary<int, bool>();
        private static Vector2 _initialMousePos;
        
        private static bool MouseInWindow()
        {
            Vector2 mp = InputTracker.MousePosition;
            Vector2 p = ImGui.GetWindowPos();
            Vector2 s = ImGui.GetWindowSize();
            if ((int)mp.X < p.X || (int)mp.X >= p.X + s.X)
            {
                return false;
            }
            if ((int)mp.Y < p.Y || (int)mp.Y >= p.Y + s.Y)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Adds the ability to pan the current window by holding the middle mouse button.
        /// </summary>
        /// <param name="id">Random number. Must be unique.</param>
        static public void PanWindowMiddleClick(int id){
            if(!_mouseCurrentlyPanning.ContainsKey(id))
            {
                _mouseCurrentlyPanning.Add(id , false);
            }

            if (InputTracker.GetMouseButtonDown(Veldrid.MouseButton.Middle) && MouseInWindow())//		Start middle mouse click starts pan
            {
                _mouseCurrentlyPanning[id] = true;
                _initialMousePos = InputTracker.MousePosition;
                Sdl2Native.SDL_ShowCursor(0);
            }
            if (_mouseCurrentlyPanning[id])
            {
                Sdl2Native.SDL_WarpMouseInWindow(Window.SdlWindowHandle, (int)_initialMousePos.X, (int)_initialMousePos.Y);
                Sdl2Native.SDL_SetWindowGrab(Window.SdlWindowHandle, false);
                ImGui.SetScrollY(ImGui.GetScrollY() + (_initialMousePos.Y - InputTracker.MousePosition.Y));
                    
                if (!InputTracker.GetMouseButton(Veldrid.MouseButton.Middle))//		Release middle mouse button
                {
                    _mouseCurrentlyPanning[id] = false;
                    Sdl2Native.SDL_ShowCursor(1);
                }
            }
        }
    }
}