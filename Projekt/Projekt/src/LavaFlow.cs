using System;
using System.Drawing;
using System.Drawing.Imaging;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using ImgPF = System.Drawing.Imaging.PixelFormat;
using GlPF  = OpenTK.Graphics.OpenGL4.PixelFormat;

namespace MyLavaRunner
{
    internal sealed class LavaFlow : IDisposable
    {
        // GPU buffers
        int  _vao, _vbo, _prog, _tex;
        bool _ok;

        // simulation state
        float _frontZ;            // front Z
        float _spd;               // speed
        const float START  =  6f; // start speed
        const float ACCEL  =  0.8f; // acceleration
        const float WIDTH  = 50f;  // track width
        const float LENGTH = 600f; // floor length
        const float WALL_H = 12f;  // wall height
        const float Y_PLANE= 0.01f; // floor height

        // public API
        public void Reset(float startZ) { _frontZ = startZ; _spd = START; }
        public bool Hits(Vector3 p)     => p.Z >= _frontZ;

        // load resources
        public void Load(string tex = "Resources/Textures/lava_texture.jpg")
        {
            if (_ok) return;

            float w = WIDTH * .5f;
            float len = LENGTH;

            /* Geometry:
               floor and wall
            */
            float[] v =
            {
                // floor
                -w, Y_PLANE, 0,    0,          0,
                 w, Y_PLANE, 0,    1,          0,
                 w, Y_PLANE, len,  1, len/25f,
                 w, Y_PLANE, len,  1, len/25f,
                -w, Y_PLANE, len,  0, len/25f,
                -w, Y_PLANE, 0,    0,          0,

                // wall
                -w, 0,       0,    0, 0,
                 w, 0,       0,    1, 0,
                 w, WALL_H,  0,    1, WALL_H/25f,
                 w, WALL_H,  0,    1, WALL_H/25f,
                -w, WALL_H,  0,    0, WALL_H/25f,
                -w, 0,       0,    0, 0,
            };

            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();
            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, v.Length * sizeof(float),
                          v, BufferUsageHint.StaticDraw);

            const int S = 5 * sizeof(float);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, S, 0);
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, S, 3*sizeof(float));

            const string vs =
                "#version 330 core\n"
              + "layout(location=0)in vec3 p;layout(location=1)in vec2 uv;"
              + "uniform mat4 M,V,P;uniform float T;out vec2 t;"
              + "void main(){t=uv+vec2(T*0.08,T*0.03);gl_Position=P*V*M*vec4(p,1);}";

            const string fs =
                "#version 330 core\n"
              + "in vec2 t;out vec4 C;uniform sampler2D Tex;"
              + "void main(){C=texture(Tex,t);}";

            _prog = Compile(vs, fs);
            GL.UseProgram(_prog);
            GL.Uniform1(GL.GetUniformLocation(_prog, "Tex"), 0);

            _tex = LoadTexture(tex);
            _ok  = true;
        }

        // update simulation
        public void Update(float dt)
        {
            _frontZ -= _spd * dt;
            _spd    += ACCEL * dt;
        }

        // draw lava
        public void Draw(Matrix4 V, Matrix4 P, float time)
        {
            if (!_ok) return;

            Matrix4 M = Matrix4.CreateTranslation(0f, 0f, _frontZ);

            bool depth = GL.IsEnabled(EnableCap.DepthTest);
            bool cull  = GL.IsEnabled(EnableCap.CullFace);
            GL.Disable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);

            GL.UseProgram(_prog);
            GL.UniformMatrix4(GL.GetUniformLocation(_prog, "M"), false, ref M);
            GL.UniformMatrix4(GL.GetUniformLocation(_prog, "V"), false, ref V);
            GL.UniformMatrix4(GL.GetUniformLocation(_prog, "P"), false, ref P);
            GL.Uniform1(GL.GetUniformLocation(_prog, "T"), time);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _tex);

            GL.BindVertexArray(_vao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 12);
            GL.BindVertexArray(0);

            if (depth) GL.Enable(EnableCap.DepthTest);
            if (cull)  GL.Enable(EnableCap.CullFace);
        }

        // shader compile
        static int Compile(string v, string f)
        {
            int vs = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vs, v); GL.CompileShader(vs);
            int fs = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fs, f); GL.CompileShader(fs);
            int p  = GL.CreateProgram();
            GL.AttachShader(p, vs); GL.AttachShader(p, fs); GL.LinkProgram(p);
            GL.DeleteShader(vs); GL.DeleteShader(fs);
            return p;
        }

        // texture loader
        static int LoadTexture(string path)
        {
            int id = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, id);

            using (var bmp = new Bitmap(path))
            {
                bmp.RotateFlip(RotateFlipType.RotateNoneFlipY);
                BitmapData d = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height),
                                            ImageLockMode.ReadOnly, ImgPF.Format32bppArgb);
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba,
                              bmp.Width, bmp.Height, 0, GlPF.Bgra,
                              PixelType.UnsignedByte, d.Scan0);
                bmp.UnlockBits(d);
            }

            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
                            (int)TextureMinFilter.LinearMipmapLinear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
                            (int)TextureMagFilter.Linear);
            return id;
        }

        // cleanup
        public void Dispose()
        {
            if (!_ok) return;
            GL.DeleteBuffer(_vbo);
            GL.DeleteVertexArray(_vao);
            GL.DeleteTexture(_tex);
            GL.DeleteProgram(_prog);
            _ok = false;
        }
    }
}
