using System;
using System.Numerics;
using Veldrid;
using Veldrid.Sdl2;

namespace StudioCore
{
    public class WorldView
    {
        //	These camera properties are used by Viewport.Draw()
        public Transform CameraTransform = Transform.Default;
        public Transform CameraPositionDefault = Transform.Default;
        public float OrbitCamDistance = 12;
        public float ModelHeight_ForOrbitCam = 1;
        public float ModelDepth_ForOrbitCam = 1;
        public Vector3 OrbitCamCenter = new Vector3(0, 0.5f, 0);

        private Rectangle BoundingRect;

        public Matrix4x4 WorldMatrixMOD = Matrix4x4.Identity;

        public MsbEditor.ProjectSettings Settings;

        public WorldView(Rectangle bounds)
        {
            BoundingRect = bounds;
        }

        public void UpdateBounds(Rectangle bounds)
        {
            BoundingRect = bounds;
        }

        public Vector3 LightRotation = Vector3.Zero;
        public Vector3 LightDirectionVector => 
            Vector3.Transform(Vector3.UnitX,
            Matrix4x4.CreateRotationY(LightRotation.Y)
            * Matrix4x4.CreateRotationZ(LightRotation.Z)
            * Matrix4x4.CreateRotationX(LightRotation.X)
            );

        public Matrix4x4 MatrixWorld;
        public Matrix4x4 MatrixProjection;

        //		The clip planes should be taken from Viewport.cs if I can get a reference to it.
        public static float NearClip = 0.1f;
        public static float FarClip = 20000;
        public static float FieldOfView = 60;

        public float CameraTurnSpeedGamepad = 1.5f * 0.1f;
        public float CameraTurnSpeedMouse = 1.5f * 0.25f;

        public float CameraMoveSpeed = 20.0f;
        public float CameraMoveSpeedFast = 200.0f;
        public float CameraMoveSpeedSlow = 1.0f;


        public void LoadSettings (MsbEditor.ProjectSettings Settings)
        {
            this.Settings = Settings;
            CameraTransform.Position = new Vector3(Settings.CameraPosX , Settings.CameraPosY , Settings.CameraPosZ);
            CameraTransform.Rotation = new Quaternion(Settings.CameraRotX , Settings.CameraRotY , Settings.CameraRotZ , Settings.CameraRotW);
            OrbitCamCenter = new Vector3(Settings.CameraOrbitCenterX , Settings.CameraOrbitCenterY , Settings.CameraOrbitCenterZ);
            OrbitCamDistance = Settings.OrbitCamDistance;
            NearClip = Settings.NearClipPlane;
            FarClip = Settings.FarClipPlane;
            FieldOfView = Settings.FieldOfView;
        }

        public void ResetCameraLocation()
        {
            CameraTransform.Position = Vector3.Zero;
            CameraTransform.Rotation = Quaternion.Identity;
        }

        public void LookAtTransform(Transform t)
        {
            var newLookDir = Vector3.Normalize(t.Position - (CameraTransform.Position));
            var eu = CameraTransform.EulerRotation;
            eu.Y = (float)Math.Atan2(-newLookDir.X, newLookDir.Z);
            eu.X = (float)Math.Asin(newLookDir.Y);
            eu.Z = 0;
            CameraTransform.EulerRotation = eu;
        }

        public void GoToTransformAndLookAtIt(Transform t, float distance)
        {
            var positionOffset = Vector3.Transform(Vector3.UnitX, t.RotationMatrix) * distance;
            CameraTransform.Position = t.Position + positionOffset;
            LookAtTransform(t);
        }

        public float GetDistanceSquaredFromCamera(Transform t)
        {
            return (t.Position - GetCameraPhysicalLocation().Position).LengthSquared();
        }

        public Vector3 ROUGH_GetPointOnFloor(Vector3 pos, Vector3 dir, float stepDist)
        {
            Vector3 result = pos;
            Vector3 nDir = Vector3.Normalize(dir);
            while (result.Y > 0)
            {
                if (result.Y >= 1)
                    result += nDir * 1;
                else
                    result += nDir * stepDist;
            }
            result.Y = 0;
            return result;
        }

        public Transform GetSpawnPointFromScreenPos(Vector2 screenPos, float distance, bool faceBackwards, bool lockPitch, bool alignToFloor)
        {
            var result = Transform.Default;
            return result;
        }

        public Transform GetCameraPhysicalLocation()
        {
            var result = Transform.Default;
            return result;
        }

        public void MoveCamera(float x, float y, float z, float speed)
        {
            CameraTransform.Position += Vector3.Transform(new Vector3(x, y, z),
                CameraTransform.Rotation
                ) * speed;
            
            if (Settings != null)//		Record camera orientation in Map Editor, not model editor
            {
                Settings.CameraPosition = CameraTransform.Position;
                Settings.CameraRotation = CameraTransform.Rotation;
                Settings.CameraOrbitCenter = OrbitCamCenter;
                Settings.OrbitCamDistance = OrbitCamDistance;
                Settings.NearClipPlane = NearClip;
                Settings.FarClipPlane = FarClip;
                Settings.FieldOfView = FieldOfView;
            }
        }

        public void UpdateOrbitCameraCenter()//		Set the OrbitCamCenter to be the position OrbitCamDistance in front of the camera
        {
            OrbitCamCenter = CameraTransform.Position + Vector3.Transform(new Vector3(0, 0, OrbitCamDistance),
                CameraTransform.Rotation);
        }

        public void RotateOrbitCamera(float h, float v, float speed)
        {
            var eu = CameraTransform.EulerRotation;
            eu.Y -= h * speed;
            eu.X = Math.Clamp(eu.X + v * speed , -1.57f , 1.57f);//		negative is looking up
            CameraTransform.EulerRotation = eu;
            CameraTransform.Position = OrbitCamCenter - Vector3.Transform(new Vector3(0, 0, OrbitCamDistance),
                CameraTransform.Rotation);
        }

        private Vector2 mousePos = Vector2.Zero;
        private Vector2 oldMouse = Vector2.Zero;
       // private int oldWheel = 0;
        private bool currentMouseClickL = false;
        private bool currentMouseClickR = false;
        private bool currentMouseClickM = false;
        private bool currentMouseClickStartedInWindow = false;
        private bool oldMouseClickL = false;
        private bool oldMouseClickR = false;
        private bool lastFrameMouseClickM = false;
        private bool shiftWasHeldBeforeClickM = false;
        private MouseClickType currentClickType = MouseClickType.None;
        private MouseClickType oldClickType = MouseClickType.None;

        private bool oldResetKeyPressed = false;

        public enum MouseClickType
        {
            None,
            Left,
            Right,
            Middle,
            Extra1,
            Extra2,
        }

        private bool MousePressed = false;//		Not sure what this is. = Mouse confined to window maybe
        private Vector2 MousePressedPos = new Vector2();

        public bool UpdateInput(Sdl2Window window, float dt)
        {

            float clampedLerpF = Utils.Clamp(30 * dt, 0, 1);

            mousePos = new Vector2(Utils.Lerp(oldMouse.X, InputTracker.MousePosition.X, clampedLerpF),
                Utils.Lerp(oldMouse.Y, InputTracker.MousePosition.Y, clampedLerpF));


            //KeyboardState keyboard = DBG.EnableKeyboardInput ? Keyboard.GetState() : DBG.DisabledKeyboardState;
            //int currentWheel = mouse.ScrollWheelValue;

            //bool mouseInWindow = MapStudio.Active && mousePos.X >= game.ClientBounds.Left && mousePos.X < game.ClientBounds.Right && mousePos.Y > game.ClientBounds.Top && mousePos.Y < game.ClientBounds.Bottom;

            currentClickType = MouseClickType.None;

            if (InputTracker.GetMouseButton(Veldrid.MouseButton.Left))
                currentClickType = MouseClickType.Left;
            else if (InputTracker.GetMouseButton(Veldrid.MouseButton.Right))
                currentClickType = MouseClickType.Right;
            else if (InputTracker.GetMouseButton(Veldrid.MouseButton.Middle))
                currentClickType = MouseClickType.Middle;
            else if (InputTracker.GetMouseButton(Veldrid.MouseButton.Button1))
                currentClickType = MouseClickType.Extra1;
            else if (InputTracker.GetMouseButton(Veldrid.MouseButton.Button2))
                currentClickType = MouseClickType.Extra2;
            else
                currentClickType = MouseClickType.None;
            
            bool isResetKeyPressed = InputTracker.GetKey(Veldrid.Key.R);

            if (isResetKeyPressed && !oldResetKeyPressed)
            {
                ResetCameraLocation();
            }

            oldResetKeyPressed = isResetKeyPressed;


            //		Zoom controls (It's up here so we don't have to mess with mouse position stuff when just zooming)
            int mouseWheel = Math.Sign(InputTracker.GetMouseWheelDelta());
            if (mouseWheel != 0)
            {
                //		Multiplying the change by OrbitCamDistance will make zooming finer when zoomed in
                OrbitCamDistance = Math.Min(Math.Max(OrbitCamDistance -0.15f*mouseWheel*OrbitCamDistance , NearClip) , FarClip);
                RotateOrbitCamera(0, 0, 0);//		Rotate nowhere to update camera distance to OrbitCamCenter
            }

            currentMouseClickL = currentClickType == MouseClickType.Left;
            currentMouseClickR = currentClickType == MouseClickType.Right;
            currentMouseClickM = currentClickType == MouseClickType.Middle;
            

            if (currentClickType != MouseClickType.None && oldClickType == MouseClickType.None)
                currentMouseClickStartedInWindow = true;

            if (currentClickType == MouseClickType.None)
            {
                // If nothing is pressed, just dont bother lerping
                //mousePos = new Vector2(mouse.X, mouse.Y);
                if (MousePressed)
                {
                    mousePos = InputTracker.MousePosition;
                    Sdl2Native.SDL_WarpMouseInWindow(window.SdlWindowHandle, (int)MousePressedPos.X, (int)MousePressedPos.Y);
                    Sdl2Native.SDL_SetWindowGrab(window.SdlWindowHandle, false);
                    Sdl2Native.SDL_ShowCursor(1);
                    MousePressed = false;
                }

                lastFrameMouseClickM = false;

                return false;
            }

            bool isSpeedupKeyPressed = InputTracker.GetKey(Veldrid.Key.LShift) || InputTracker.GetKey(Veldrid.Key.RShift);
            bool isSlowdownKeyPressed = InputTracker.GetKey(Veldrid.Key.LControl) || InputTracker.GetKey(Veldrid.Key.RControl);
            bool isMoveLightKeyPressed = InputTracker.GetKey(Veldrid.Key.Space);


            if (!currentMouseClickStartedInWindow)
            {
                oldMouse = mousePos;

                var euler = CameraTransform.EulerRotation;
                euler.X = Utils.Clamp(CameraTransform.EulerRotation.X, -Utils.PiOver2, Utils.PiOver2);
                CameraTransform.EulerRotation = euler;
             //   }

                LightRotation.X = Utils.Clamp(LightRotation.X, -Utils.PiOver2, Utils.PiOver2);

                oldClickType = currentClickType;

                oldMouseClickL = currentMouseClickL;
                oldMouseClickR = currentMouseClickR;
                lastFrameMouseClickM = currentMouseClickM;

                return true;
            }


            float moveMult = dt * CameraMoveSpeed;

            if (isSpeedupKeyPressed)
            {
                moveMult = dt * CameraMoveSpeedFast;
            }

            if (isSlowdownKeyPressed)
            {
                moveMult = dt * CameraMoveSpeedSlow;
            }
            
           
            float x = 0;
            float y = 0;
            float z = 0;

            if (InputTracker.GetKey(Veldrid.Key.D))
                x += 1;
            if (InputTracker.GetKey(Veldrid.Key.A))
                x -= 1;
            if (InputTracker.GetKey(Veldrid.Key.E))
                y += 1;
            if (InputTracker.GetKey(Veldrid.Key.Q))
                y -= 1;
            if (InputTracker.GetKey(Veldrid.Key.W))
                z += 1;
            if (InputTracker.GetKey(Veldrid.Key.S))
                z -= 1;

            MoveCamera(x, y, z, moveMult);
            UpdateOrbitCameraCenter();
            
           
            if (currentMouseClickR || currentMouseClickM)
            {
                if (!MousePressed)//		First frame of mouse press
                {
                    var mx = InputTracker.MousePosition.X;
                    var my = InputTracker.MousePosition.Y;
                    if (mx >= BoundingRect.Left && mx < BoundingRect.Right && my >= BoundingRect.Top && my < BoundingRect.Bottom)
                    {
                        MousePressed = true;
                        MousePressedPos = InputTracker.MousePosition;
                        Sdl2Native.SDL_ShowCursor(0);
                        Sdl2Native.SDL_SetWindowGrab(window.SdlWindowHandle, true);
                    }
                    
                    if (currentMouseClickM && !lastFrameMouseClickM)//		First frame ClickM is held
                    {
                        shiftWasHeldBeforeClickM = isSpeedupKeyPressed;//		Record shift's state. The camera will orbit/pan based only on the initial state of shift when ClickM is pressed
                    }
                }
                else
                {
                    Vector2 mouseDelta = MousePressedPos - InputTracker.MousePosition;
                    Sdl2Native.SDL_WarpMouseInWindow(window.SdlWindowHandle, (int)MousePressedPos.X, (int)MousePressedPos.Y);

                    //Mouse.SetPosition(game.ClientBounds.X + game.ClientBounds.Width / 2, game.ClientBounds.Y + game.ClientBounds.Height / 2);

                    float camH = mouseDelta.X * 1 * CameraTurnSpeedMouse * 0.0160f;
                    float camV = mouseDelta.Y * -1 * CameraTurnSpeedMouse * 0.0160f;
                    

                    if (currentMouseClickR)//		Look Camera
                    {

                        if (mouseDelta.LengthSquared() == 0)
                        {
                            // Prevents a meme
                            //oldWheel = currentWheel;
                            return true;
                        }

                        if (isMoveLightKeyPressed)
                        {
                            LightRotation.Y += camH;
                            LightRotation.X -= camV;
                        }
                        else
                        {
                            var eul = CameraTransform.EulerRotation;
                            eul.Y -= camH;
                            eul.X += camV;
                            CameraTransform.EulerRotation = eul;
                            UpdateOrbitCameraCenter();
                        }
                    }
                    else if (currentMouseClickM)//	Orbit camera
                    {
                        if (shiftWasHeldBeforeClickM){//	Pan
                            Vector3 cameraSpacePanDirection = new Vector3(camH, camV, 0);
                        //	Vector3 cameraSpacePanDirection = Vector3.Transform(new Vector3(camH, camV, 0), CameraTransform.Rotation);
                        //		CameraMoveSpeed is not used so Msb and Model Editors work the same and movement speed is based on zoom. I hard coded 10 instead.
                            MoveCamera(cameraSpacePanDirection.X, cameraSpacePanDirection.Y, cameraSpacePanDirection.Z, OrbitCamDistance * dt * 10);
                            UpdateOrbitCameraCenter();
                        }
                        else//		Orbit
                        {
                            RotateOrbitCamera(camH, camV, Utils.PiOver2);
                        }
                    }
                }


                //CameraTransform.Rotation.Z -= (float)Math.Cos(MathHelper.PiOver2 - CameraTransform.Rotation.Y) * camV;

                //RotateCamera(mouseDelta.Y * -0.01f * (float)moveMult, 0, 0, moveMult);
                //RotateCamera(0, mouseDelta.X * 0.01f * (float)moveMult, 0, moveMult);
            }
            else
            {
                if (MousePressed)
                {
                    Sdl2Native.SDL_WarpMouseInWindow(window.SdlWindowHandle, (int)MousePressedPos.X, (int)MousePressedPos.Y);
                    Sdl2Native.SDL_SetWindowGrab(window.SdlWindowHandle, false);
                    Sdl2Native.SDL_ShowCursor(1);
                    MousePressed = false;
                }

                if (oldMouseClickL)
                {
                    //Mouse.SetPosition((int)oldMouse.X, (int)oldMouse.Y);
                }
                //game.IsMouseVisible = true;
            }



            var eu = CameraTransform.EulerRotation;
            eu.X = Utils.Clamp(CameraTransform.EulerRotation.X, -Utils.PiOver2, Utils.PiOver2);
            CameraTransform.EulerRotation = eu;


            LightRotation.X = Utils.Clamp(LightRotation.X, -Utils.PiOver2, Utils.PiOver2);

            oldClickType = currentClickType;

            oldMouseClickL = currentMouseClickL;
            oldMouseClickR = currentMouseClickR;
            lastFrameMouseClickM = currentMouseClickM;

            oldMouse = mousePos;
            return true;
        }
    }
}