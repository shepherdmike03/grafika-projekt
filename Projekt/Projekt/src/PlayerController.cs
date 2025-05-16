using System;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace MyLavaRunner
{
    internal sealed class PlayerController
    {
        public Vector3 Pos { get; private set; }
        public Vector3 Velocity => _vel;

        const float Walk = 5;
        const float Jump = 8;
        const float Grav = -20;
        const float DeathY = -20;
        const float HalfW = 24;
        const float MaxMS = 500f / 3.6f; // 138.888 m/s

        Vector3 _vel;
        float _sprintT;
        bool _dead;
        double _timer;

        public float SpeedKmh => new Vector2(_vel.X, _vel.Z).Length * 3.6f;

        public void Update(FrameEventArgs e, KeyboardState k)
        {
            if (_dead)
            {
                if ((_timer -= e.Time) <= 0) Respawn();
                return;
            }

            float dt = (float)e.Time;

            // sprint
            bool holdShift = k.IsKeyDown(Keys.LeftShift) || k.IsKeyDown(Keys.RightShift);
            _sprintT = holdShift ? _sprintT + dt : 0;

            float mult = 1f;
            if (holdShift) mult = MathF.Pow(2f, 1f + _sprintT / 5f);

            // move
            Vector3 dir = Vector3.Zero;
            if (k.IsKeyDown(Keys.W)) dir.Z -= 1;
            if (k.IsKeyDown(Keys.S)) dir.Z += 1;
            if (k.IsKeyDown(Keys.A)) dir.X -= 1;
            if (k.IsKeyDown(Keys.D)) dir.X += 1;
            if (dir.LengthSquared > 0) dir.Normalize();

            _vel.X = dir.X * Walk * mult;
            _vel.Z = dir.Z * Walk * mult;

            // clamp
            float h = new Vector2(_vel.X, _vel.Z).Length;
            if (h > MaxMS)
            {
                float s = MaxMS / h;
                _vel.X *= s;
                _vel.Z *= s;
            }

            // gravity & jump
            Vector3 p = Pos;
            bool ground = p.Y <= 0.001f;
            if (ground)
            {
                p.Y = 0;
                _vel.Y = 0;
            }

            if (ground && k.IsKeyPressed(Keys.Space)) _vel.Y = Jump;
            _vel.Y += Grav * dt;
            p += _vel * dt;

            // walls
            if (p.X < -HalfW)
            {
                p.X = -HalfW;
                _sprintT = 0;
            }
            else if (p.X > HalfW)
            {
                p.X = HalfW;
                _sprintT = 0;
            }

            // lava fall death
            if (p.Y < DeathY)
            {
                _dead = true;
                _timer = 2;
            }

            Pos = p;
        }

        public void Respawn() // ← now public!
        {
            Pos = Vector3.Zero;
            _vel = Vector3.Zero;
            _dead = false;
            _sprintT = 0;
        }
    }
}