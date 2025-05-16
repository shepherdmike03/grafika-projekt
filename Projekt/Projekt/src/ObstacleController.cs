using System;
using System.Collections.Generic;
using OpenTK.Mathematics;

namespace MyLavaRunner
{
    internal enum ObstacleType { Jump, Full }

    internal sealed class ObstacleController : IDisposable
    {
        struct Obstacle { public ObstacleType Type; public Vector3 Pos; }

        // lanes
        static readonly float[] LANE_X = { -20, -10, 0, 10, 20 };

        // visible sizes
        const float J_HALF_X = 4.5f, F_HALF_X = 5.5f;
        const float HALF_Z  = 1.0f;

        // hit-box extents
        const float COLL_X = 5f, COLL_Z = 2f;

        // heights
        const float J_H = 1f, F_H = 6f;

        readonly List<Obstacle> _live = new();
        readonly Random         _rng  = new();

        Model _mJump, _mFull;
        bool  _ok;
        float _nextZ = -60f;

        public void Load()
        {
            if (_ok) return;

            _mJump = new Model(); _mJump.Load("Resources/Models/Road Barrier 01a.obj");
            _mFull = new Model(); _mFull.Load("Resources/Models/Road Barrier 01b.obj");

            _ok = true;
        }

        public void Update(float dt, float pZ)
        {
            if (pZ - _nextZ <= 100f)
            {
                _live.Add(new Obstacle {
                    Type = _rng.NextDouble() < 0.4 ? ObstacleType.Jump : ObstacleType.Full,
                    Pos  = new Vector3(LANE_X[_rng.Next(LANE_X.Length)], 0, _nextZ)
                });
                _nextZ -= MathF.Max(45f + pZ * 0.025f, 20f);
            }
            for (int i = _live.Count - 1; i >= 0; --i)
                if (_live[i].Pos.Z - pZ > COLL_Z + 2f) _live.RemoveAt(i);
        }

        public void Draw(Matrix4 V, Matrix4 P)
        {
            foreach (var o in _live)
            {
                bool jump = o.Type == ObstacleType.Jump;
                float h  = jump ? J_H     : F_H;
                float hx = jump ? J_HALF_X: F_HALF_X;

                Model m  = jump ? _mJump : _mFull;

                //ground-align using model.Min.Y 
                float yOffset = -m.Min.Y * (h / (m.Max.Y - m.Min.Y));

                Matrix4 M =
                    Matrix4.CreateScale(hx * 2, h, HALF_Z * 2) *
                    Matrix4.CreateTranslation(0, yOffset, 0) *     // align base to 0
                    Matrix4.CreateTranslation(o.Pos);

                m.Draw(M, V, P);
            }
        }

        public bool CheckCollision(Vector3 p, out Vector3 hit)
        {
            hit = Vector3.Zero;
            foreach (var o in _live)
            {
                if (MathF.Abs(p.X - o.Pos.X) > COLL_X) continue;
                if (MathF.Abs(p.Z - o.Pos.Z) > COLL_Z) continue;
                if (o.Type == ObstacleType.Jump && p.Y > J_H + .2f) continue;
                hit = o.Pos; return true;
            }
            return false;
        }

        public void RemoveAt(Vector3 pos)
        {
            for (int i=0;i<_live.Count;++i)
                if ((_live[i].Pos-pos).LengthSquared<.1f){_live.RemoveAt(i);break;}
        }

        public void Reset(){ _live.Clear(); _nextZ=-60f; }
        public void Dispose(){ if(!_ok)return; _mJump.Dispose(); _mFull.Dispose(); _ok=false; }
    }
}
