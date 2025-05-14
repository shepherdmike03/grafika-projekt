// chase cam
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
        Vector3 _up    =  Vector3.UnitY;
        float   _yaw   = 0f;
        float   _pitch = 15f;
        Vector2 _prev;
        bool    _init;

        // tune
        public  float Sens   = 0.2f;
        readonly float _eye  = 1.6f;
        readonly float _boom = 6f;
        readonly float _minP = -5f;
        readonly float _maxP = 45f;

        // in
        public Vector3 Target { private get; set; }

        // out
        public Matrix4 View  => Matrix4.LookAt(_pos, Target + new Vector3(0, _eye, 0), _up);
        public Matrix4 Proj(float a) =>
            Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(45f), a, .1f, 200f);

        // tick
        public void Update(FrameEventArgs e, MouseState m, bool focused)
        {
            if (!_init){ _prev=m.Position; _init=true; }

            Vector2 d = m.Position - _prev; _prev = m.Position;
            if (focused)
            {
                _yaw   += d.X * Sens;
                _pitch  = MathHelper.Clamp(_pitch - d.Y * Sens, _minP, _maxP);
            }

            _front.X =  MathF.Cos(MathHelper.DegreesToRadians(_yaw)) *
                        MathF.Cos(MathHelper.DegreesToRadians(_pitch));
            _front.Y =  MathF.Sin(MathHelper.DegreesToRadians(_pitch));
            _front.Z =  MathF.Sin(MathHelper.DegreesToRadians(_yaw)) *
                        MathF.Cos(MathHelper.DegreesToRadians(_pitch));
            _front.Normalize();

            Vector3 focus = Target + new Vector3(0,_eye,0);
            _pos = focus - _front * _boom;

            // keep above ground
            if (_pos.Y < 0.2f) _pos.Y = 0.2f;
        }
    }
}