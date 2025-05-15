using System;
using System.Drawing;
using System.Drawing.Imaging;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using ImgPF = System.Drawing.Imaging.PixelFormat;
using GlPF = OpenTK.Graphics.OpenGL4.PixelFormat;

namespace MyLavaRunner
{
    internal sealed class UIManager : IDisposable
    {
        int _vao, _vbo, _tex, _prog;
        bool _ok;
        int _w, _h;
        const int TW = 256, TH = 40;
        string _last = string.Empty;

        
        public void Load(int w, int h)
        {
            if (_ok) return;
            _w = w;
            _h = h;

            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();
            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, 6 * 4 * sizeof(float),
                IntPtr.Zero, BufferUsageHint.DynamicDraw);

            const int s = 4 * sizeof(float);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, s, 0);
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, s, 2 * sizeof(float));

            UpdateQuad();

            const string vs =
                "#version 330 core\nlayout(location=0)in vec2 p;layout(location=1)in vec2 uv;"
                + "out vec2 t;uniform mat4 P;void main(){t=uv;gl_Position=P*vec4(p,0,1);}";

            const string fs =
                "#version 330 core\nin vec2 t;out vec4 C;uniform sampler2D T;"
                + "void main(){C=texture(T,t);}";

            _prog = Compile(vs, fs);
            GL.UseProgram(_prog);
            GL.Uniform1(GL.GetUniformLocation(_prog, "T"), 0);
            Matrix4 ortho = Matrix4.CreateOrthographicOffCenter(0, w, h, 0, -1, 1);
            GL.UniformMatrix4(GL.GetUniformLocation(_prog, "P"), false, ref ortho);

            _tex = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _tex);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
                (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
                (int)TextureMagFilter.Linear);

            UpdateText("Speed: 0 km/h");
            _ok = true;
        }

        public void Resize(int w, int h)
        {
            if (!_ok) return;
            _w = w;
            _h = h;
            UpdateQuad();
            Matrix4 ortho = Matrix4.CreateOrthographicOffCenter(0, w, h, 0, -1, 1);
            GL.UseProgram(_prog);
            GL.UniformMatrix4(GL.GetUniformLocation(_prog, "P"), false, ref ortho);
        }

        public void UpdateText(string txt)
        {
            if (!_ok || txt == _last) return;
            _last = txt;

            using var bmp = new Bitmap(TW, TH, ImgPF.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                using var f = new Font("Consolas", 22, FontStyle.Bold, GraphicsUnit.Pixel);
                g.DrawString(txt, f, Brushes.White, 0, 0);
            }

            BitmapData d = bmp.LockBits(new Rectangle(0, 0, TW, TH),
                ImageLockMode.ReadOnly, ImgPF.Format32bppArgb);
            GL.BindTexture(TextureTarget.Texture2D, _tex);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba,
                TW, TH, 0, GlPF.Bgra, PixelType.UnsignedByte, d.Scan0);
            bmp.UnlockBits(d);
        }

        public void Draw()
        {
            if (!_ok) return;

            bool depth = GL.IsEnabled(EnableCap.DepthTest);
            GL.Disable(EnableCap.DepthTest);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            GL.UseProgram(_prog);
            GL.BindTexture(TextureTarget.Texture2D, _tex);
            GL.BindVertexArray(_vao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);

            if (depth) GL.Enable(EnableCap.DepthTest);
        }

        public void Dispose()
        {
            if (!_ok) return;
            GL.DeleteBuffer(_vbo);
            GL.DeleteVertexArray(_vao);
            GL.DeleteTexture(_tex);
            GL.DeleteProgram(_prog);
            _ok = false;
        }

        
        void UpdateQuad()
        {
            float x0 = _w - TW, y0 = 0;
            float x1 = _w, y1 = TH;

            //alligned correctly
            float[] q =
            {
                x0, y0, 0, 0, x0, y1, 0, 1, x1, y1, 1, 1,
                x1, y1, 1, 1, x1, y0, 1, 0, x0, y0, 0, 0
            };
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, q.Length * sizeof(float), q);
        }

        static int Compile(string v, string f)
        {
            int V = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(V, v);
            GL.CompileShader(V);
            int F = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(F, f);
            GL.CompileShader(F);
            int P = GL.CreateProgram();
            GL.AttachShader(P, V);
            GL.AttachShader(P, F);
            GL.LinkProgram(P);
            GL.DeleteShader(V);
            GL.DeleteShader(F);
            return P;
        }
    }
}