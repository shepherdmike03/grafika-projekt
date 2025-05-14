// player move

using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace MyLavaRunner
{
    internal sealed class PlayerController
    {
        public Vector3 Pos { get; private set; }

        Vector3 _vel; // vel
        const float Walk = 5;
        const float Jump = 8;
        const float Grav = -20;
        const float DeathY = -20;
        const float HalfW = 24;

        float _sprintT; // time
        bool _dead;
        double _timer;

        public void Update(FrameEventArgs e, KeyboardState k)
        {
            if (_dead)
            {
                if ((_timer -= e.Time) <= 0) Respawn();
                return;
            }

            float dt = (float)e.Time;

            // sprint time
            bool shift = k.IsKeyDown(Keys.LeftShift) || k.IsKeyDown(Keys.RightShift);
            _sprintT = shift ? _sprintT + dt : 0;

            int stage = 0;
            if (_sprintT >= 3f) stage = 1 + (int)((_sprintT - 3f) / 5f);
            float mult = shift ? MathF.Pow(2, stage + 1) : 1;

            // move dir
            Vector3 dir = Vector3.Zero;
            if (k.IsKeyDown(Keys.W)) dir.Z -= 1;
            if (k.IsKeyDown(Keys.S)) dir.Z += 1;
            if (k.IsKeyDown(Keys.A)) dir.X -= 1;
            if (k.IsKeyDown(Keys.D)) dir.X += 1;
            if (dir.LengthSquared > 0) dir.Normalize();

            _vel.X = dir.X * Walk * mult;
            _vel.Z = dir.Z * Walk * mult;

            // jump / grav
            Vector3 p = Pos; // copy
            bool ground = p.Y <= 0.001f;
            if (ground)
            {
                p.Y = 0;
                _vel.Y = 0;
            }

            if (ground && k.IsKeyPressed(Keys.Space)) _vel.Y = Jump;
            _vel.Y += Grav * dt;
            p += _vel * dt;

            // wall clamp
            bool hit = false;
            if (p.X < -HalfW)
            {
                p.X = -HalfW;
                hit = true;
            }
            else if (p.X > HalfW)
            {
                p.X = HalfW;
                hit = true;
            }

            if (hit) _sprintT = 0;

            // death
            if (p.Y < DeathY)
            {
                _dead = true;
                _timer = 2;
            }

            Pos = p; // commit
        }

        void Respawn()
        {
            Pos = Vector3.Zero;
            _vel = Vector3.Zero;
            _dead = false;
            _sprintT = 0;
        }
    }
}