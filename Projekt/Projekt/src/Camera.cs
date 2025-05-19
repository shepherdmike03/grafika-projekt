using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace MyLavaRunner
{
    public sealed class Camera
    {
        // state
        Vector3 _pos;
        Vector3 _front = -Vector3.UnitZ;
        Vector3 _up    = Vector3.UnitY;
        float   _yaw   = 0f;
        float   _pitch = 15f;
        Vector2 _prev;
        bool    _init;
        bool    _flipped; // flipped view?

        
        // config
        public float Sens = 0.2f;
        readonly float _eye   = 1.6f;
        readonly float _boom  = 6f;
        readonly float _minP  = -5f;
        readonly float _maxP  = 45f;

        // target set
        public Vector3 Target { private get; set; }

        // screen aspect
        public float Aspect { get; set; } = 1.0f;

        // view matrix
        public Matrix4 View => Matrix4.LookAt(
            _pos,
            Target + new Vector3(0, _eye, 0),
            _up
        );

        // projection matrix
        public Matrix4 Proj(float a) =>
            Matrix4.CreatePerspectiveFieldOfView(
                MathHelper.DegreesToRadians(45f),
                a, .1f, 200f
            );

        // main update
        public void Update(FrameEventArgs e, MouseState m, bool focused, KeyboardState k)
        {
            if (!_init)
            {
                _prev = m.Position;
                _init = true;
            }

            // flip camera?
            _flipped = k.IsKeyDown(Keys.Q);

            // get mouse delta
            Vector2 delta = m.Position - _prev;
            _prev = m.Position;

            // update angles
            if (focused)
            {
                _yaw   += delta.X * Sens;
                _pitch = MathHelper.Clamp(_pitch - delta.Y * Sens, _minP, _maxP);
            }

            // add flip angle
            float usedYaw = _yaw + (_flipped ? 180f : 0f);

            // update front
            _front.X = MathF.Cos(MathHelper.DegreesToRadians(usedYaw)) *
                       MathF.Cos(MathHelper.DegreesToRadians(_pitch));
            _front.Y = MathF.Sin(MathHelper.DegreesToRadians(_pitch));
            _front.Z = MathF.Sin(MathHelper.DegreesToRadians(usedYaw)) *
                       MathF.Cos(MathHelper.DegreesToRadians(_pitch));
            _front.Normalize();

            // position camera
            Vector3 focus = Target + new Vector3(0, _eye, 0);
            _pos = focus - _front * _boom;

            // stay above ground
            if (_pos.Y < 0.2f)
                _pos.Y = 0.2f;
        }
    }
}
