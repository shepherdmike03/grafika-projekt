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
        // gl objects
        int _vao, _vbo, _tex, _prog;
        bool _ok;

        
        int _viewW, _viewH; // current OpenGL viewport size
        int _texW, _texH; // current texture dimensions
        string _last = string.Empty;

        // load
        public void Load(int viewW, int viewH)
        {
            if (_ok) return;
            _viewW = viewW;
            _viewH = viewH;

            // geometry
            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();
            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, 6 * 4 * sizeof(float),
                IntPtr.Zero, BufferUsageHint.DynamicDraw);

            const int stride = 4 * sizeof(float);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, 0);
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, 2 * sizeof(float));

            // shader
            const string vs = "#version 330 core\n" +
                              "layout(location=0)in vec2 p;layout(location=1)in vec2 uv;" +
                              "out vec2 t;uniform mat4 P;" +
                              "void main(){t=uv;gl_Position=P*vec4(p,0,1);}";

            const string fs = "#version 330 core\n" +
                              "in vec2 t;out vec4 C;uniform sampler2D T;" +
                              "void main(){C=texture(T,t);}";

            _prog = Compile(vs, fs);
            GL.UseProgram(_prog);
            GL.Uniform1(GL.GetUniformLocation(_prog, "T"), 0);

            // 2D ortho projection (screen space)
            Matrix4 ortho = Matrix4.CreateOrthographicOffCenter(0, viewW, viewH, 0, -1, 1);
            GL.UniformMatrix4(GL.GetUniformLocation(_prog, "P"), false, ref ortho);

            // texture 
            _tex = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _tex);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
                (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
                (int)TextureMagFilter.Linear);

            // allocate a minimal starting texture
            AllocTexture(256, 40);

            UpdateText("Speed: 0 km/h"); // initial HUD
            _ok = true;
        }

        // resize
        public void Resize(int viewW, int viewH)
        {
            if (!_ok) return;

            _viewW = viewW;
            _viewH = viewH;
            UpdateQuad(); // move quad to new RHS corner

            Matrix4 ortho = Matrix4.CreateOrthographicOffCenter(0, viewW, viewH, 0, -1, 1);
            GL.UseProgram(_prog);
            GL.UniformMatrix4(GL.GetUniformLocation(_prog, "P"), false, ref ortho);
        }

        // update text
        public void UpdateText(string txt)
        {
            if (!_ok || txt == _last) return;
            _last = txt;

            // measure how big the string really is
            SizeF textSize;
            using (var tmpBmp = new Bitmap(1, 1))
            using (Graphics g = Graphics.FromImage(tmpBmp))
            using (Font font = new Font("Consolas", 22, FontStyle.Bold, GraphicsUnit.Pixel))
            {
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                textSize = g.MeasureString(txt, font);
            }

            int newW = (int)Math.Ceiling(textSize.Width) + 8; // padding
            int newH = (int)Math.Ceiling(textSize.Height) + 4;

            // reallocate if too small
            if (newW > _texW || newH > _texH)
            {
                AllocTexture(newW, newH);
                UpdateQuad();
            }

            // render into bitmap
            using (var bmp = new Bitmap(_texW, _texH, ImgPF.Format32bppArgb))
            using (Graphics g = Graphics.FromImage(bmp))
            using (Font font = new Font("Consolas", 22, FontStyle.Bold, GraphicsUnit.Pixel))
            {
                g.Clear(Color.Transparent);
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                g.DrawString(txt, font, Brushes.White, 0, 0);

                BitmapData d = bmp.LockBits(new Rectangle(0, 0, _texW, _texH),
                    ImageLockMode.ReadOnly, ImgPF.Format32bppArgb);

                GL.BindTexture(TextureTarget.Texture2D, _tex);
                GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0,
                    _texW, _texH, GlPF.Bgra, PixelType.UnsignedByte, d.Scan0);
                bmp.UnlockBits(d);
            }
        }

        // draw
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

        //cleanup
        public void Dispose()
        {
            if (!_ok) return;
            GL.DeleteBuffer(_vbo);
            GL.DeleteVertexArray(_vao);
            GL.DeleteTexture(_tex);
            GL.DeleteProgram(_prog);
            _ok = false;
        }

        // helpers
        void AllocTexture(int w, int h)
        {
            _texW = w;
            _texH = h;

            GL.BindTexture(TextureTarget.Texture2D, _tex);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba,
                w, h, 0, GlPF.Bgra, PixelType.UnsignedByte, IntPtr.Zero);
        }

        void UpdateQuad()
        {
            float x0 = _viewW - _texW, y0 = 0;
            float x1 = _viewW, y1 = _texH;

            float[] q =
            {
                x0, y0, 0, 0,
                x0, y1, 0, 1,
                x1, y1, 1, 1,

                x1, y1, 1, 1,
                x1, y0, 1, 0,
                x0, y0, 0, 0
            };

            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero,
                q.Length * sizeof(float), q);
        }

        static int Compile(string v, string f)
        {
            int vs = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vs, v);
            GL.CompileShader(vs);

            int fs = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fs, f);
            GL.CompileShader(fs);

            int p = GL.CreateProgram();
            GL.AttachShader(p, vs);
            GL.AttachShader(p, fs);
            GL.LinkProgram(p);

            GL.DeleteShader(vs);
            GL.DeleteShader(fs);
            return p;
        }
    }
}