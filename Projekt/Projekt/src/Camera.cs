using System;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace MyLavaRunner
{
    
    public sealed class Camera
    {
        // state
        private Vector3 _position;
        private Vector3 _front = -Vector3.UnitZ;
        private Vector3 _up    =  Vector3.UnitY;
        private float   _yaw   = -90f;
        private float   _pitch =   0f;

        // tuning
        public float MoveSpeed { get; set; } = 5f;
        public float MouseSens { get; set; } = 0.20f;

        // ctor
        public Camera(Vector3 startPos) => _position = startPos;

        public Matrix4 ViewMatrix => Matrix4.LookAt(_position, _position + _front, _up);

        public Matrix4 Projection(float aspect,
                                  float fovDeg = 45f,
                                  float zNear  = 0.1f,
                                  float zFar   = 100f)
            => Matrix4.CreatePerspectiveFieldOfView(
                   MathHelper.DegreesToRadians(fovDeg), aspect, zNear, zFar);

        // per frame update
        public void Update(FrameEventArgs e,
                           KeyboardState kb,
                           MouseState mouse,
                           bool windowFocused)
        {
            float dt = (float)e.Time;

            //  movement
            if (windowFocused)
            {
                if (kb.IsKeyDown(Keys.W)) _position += _front * MoveSpeed * dt;
                if (kb.IsKeyDown(Keys.S)) _position -= _front * MoveSpeed * dt;

                Vector3 right = Vector3.Normalize(Vector3.Cross(_front, _up));
                if (kb.IsKeyDown(Keys.D)) _position += right * MoveSpeed * dt;
                if (kb.IsKeyDown(Keys.A)) _position -= right * MoveSpeed * dt;
            }

            // mouse‑look 
            if (!_mouseInit)
            {
                _prevMousePos = mouse.Position;
                _mouseInit = true;
                return;                     // skip first frame to avoid jump
            }

            Vector2 delta = mouse.Position - _prevMousePos;
            _prevMousePos = mouse.Position;

            if (!windowFocused) return;     // ignore while unfocused

            _yaw   += delta.X * MouseSens;
            _pitch -= delta.Y * MouseSens;
            _pitch  = MathHelper.Clamp(_pitch, -89f, 89f);

            Vector3 dir;
            dir.X = MathF.Cos(MathHelper.DegreesToRadians(_yaw)) *
                    MathF.Cos(MathHelper.DegreesToRadians(_pitch));
            dir.Y = MathF.Sin(MathHelper.DegreesToRadians(_pitch));
            dir.Z = MathF.Sin(MathHelper.DegreesToRadians(_yaw)) *
                    MathF.Cos(MathHelper.DegreesToRadians(_pitch));

            _front = Vector3.Normalize(dir);
        }

        private bool   _mouseInit    = false;
        private Vector2 _prevMousePos;
    }
}
