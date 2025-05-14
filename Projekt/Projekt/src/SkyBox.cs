using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

using ImgPF = System.Drawing.Imaging.PixelFormat;
using GlPF  = OpenTK.Graphics.OpenGL4.PixelFormat;

namespace MyLavaRunner
{
    internal sealed class SkyBox : IDisposable
    {
        // GPU buffers
        private int _vao, _vbo;
        // Cubemap texture
        private int _cubeMap;
        // Shader program
        private int _prog;
        // Init flag
        private bool _ok;

        // Cube vertices
        private static readonly float[] Cube =
        {
            -1f,  1f, -1f,  -1f, -1f, -1f,   1f, -1f, -1f,
             1f, -1f, -1f,   1f,  1f, -1f,  -1f,  1f, -1f,

            -1f, -1f,  1f,  -1f, -1f, -1f,  -1f,  1f, -1f,
            -1f,  1f, -1f,  -1f,  1f,  1f,  -1f, -1f,  1f,

             1f, -1f, -1f,   1f, -1f,  1f,   1f,  1f,  1f,
             1f,  1f,  1f,   1f,  1f, -1f,   1f, -1f, -1f,

            -1f, -1f,  1f,  -1f,  1f,  1f,   1f,  1f,  1f,
             1f,  1f,  1f,   1f, -1f,  1f,  -1f, -1f,  1f,

            -1f,  1f, -1f,   1f,  1f, -1f,   1f,  1f,  1f,
             1f,  1f,  1f,  -1f,  1f,  1f,  -1f,  1f, -1f,

            -1f, -1f, -1f,  -1f, -1f,  1f,   1f, -1f, -1f,
             1f, -1f, -1f,  -1f, -1f,  1f,   1f, -1f,  1f
        };

        // Vertex shader
        private const string VS = @"#version 330 core
layout(location = 0) in vec3 aPosition;
out vec3 TexCoords;
uniform mat4 uView;
uniform mat4 uProjection;
void main()
{
    TexCoords   = aPosition;
    vec4 pos    = uProjection * uView * vec4(aPosition, 1.0);
    gl_Position = pos.xyww;
}";

        // Fragment shader
        private const string FS = @"#version 330 core
in  vec3 TexCoords;
out vec4 FragColor;
uniform samplerCube skybox;
void main()
{
    FragColor = texture(skybox, TexCoords);
}";

        // Load skybox
        public void Load(string dir = "Resources/Textures/skybox/")
        {
            if (_ok) return;

            // Load geometry
            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();
            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, Cube.Length * sizeof(float),
                          Cube, BufferUsageHint.StaticDraw);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float,
                                   false, 3 * sizeof(float), 0);
            GL.BindVertexArray(0);

            // Load shaders
            _prog = CreateShader(VS, FS);
            GL.UseProgram(_prog);
            GL.Uniform1(GL.GetUniformLocation(_prog, "skybox"), 0);
            GL.Enable(EnableCap.TextureCubeMapSeamless);

            // Load textures
            string[] faces =
            {
                dir + "right.jpg",
                dir + "left.jpg",
                dir + "top.jpg",
                dir + "bottom.jpg",
                dir + "front.jpg",
                dir + "back.jpg"
            };
            _cubeMap = LoadCubemapCropped(faces);

            _ok = true;
        }

        // Draw skybox
        public void Draw(Matrix4 view, Matrix4 proj)
        {
            if (!_ok) return;

            // Remove position
            view.Row3.Xyz = Vector3.Zero;

            // Save depth state
            var prevDepthFunc = (DepthFunction)GL.GetInteger(GetPName.DepthFunc);
            bool prevDepthMask = GL.GetBoolean(GetPName.DepthWritemask);

            GL.DepthMask(false);
            GL.DepthFunc(DepthFunction.Lequal);

            GL.UseProgram(_prog);
            GL.UniformMatrix4(GL.GetUniformLocation(_prog, "uView"),       false, ref view);
            GL.UniformMatrix4(GL.GetUniformLocation(_prog, "uProjection"), false, ref proj);

            GL.BindVertexArray(_vao);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.TextureCubeMap, _cubeMap);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 36);
            GL.BindVertexArray(0);

            // Restore depth state
            GL.DepthMask(prevDepthMask);
            GL.DepthFunc(prevDepthFunc);
        }

        // Build shaders
        private static int CreateShader(string vsSrc, string fsSrc)
        {
            int vs = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vs, vsSrc);
            GL.CompileShader(vs);
            CheckShader(vs, "vertex");

            int fs = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fs, fsSrc);
            GL.CompileShader(fs);
            CheckShader(fs, "fragment");

            int prog = GL.CreateProgram();
            GL.AttachShader(prog, vs);
            GL.AttachShader(prog, fs);
            GL.LinkProgram(prog);
            GL.GetProgram(prog, GetProgramParameterName.LinkStatus, out int ok);
            if (ok == 0) throw new Exception("Program link: " + GL.GetProgramInfoLog(prog));

            GL.DeleteShader(vs);
            GL.DeleteShader(fs);
            return prog;

            // Check compile
            static void CheckShader(int id, string tag)
            {
                GL.GetShader(id, ShaderParameter.CompileStatus, out int ok);
                if (ok == 0) throw new Exception($"{tag} shader: {GL.GetShaderInfoLog(id)}");
            }
        }

        // Load cube textures
        private static int LoadCubemapCropped(string[] faces)
        {
            var bitmaps = faces.Select(f => new Bitmap(f)).ToList();

            foreach (var (bmp, idx) in bitmaps.Select((b, i) => (b, i)))
                Console.WriteLine($"{Path.GetFileName(faces[idx])}: {bmp.Width}×{bmp.Height}");

            int side = bitmaps.Min(b => Math.Min(b.Width, b.Height));
            Console.WriteLine($"Cropping all faces to {side}×{side}");

            int tex = GL.GenTexture();
            GL.BindTexture(TextureTarget.TextureCubeMap, tex);

            for (int i = 0; i < faces.Length; i++)
            {
                var orig = bitmaps[i];
                int x = (orig.Width  - side) / 2;
                int y = (orig.Height - side) / 2;

                using var square = orig.Clone(new Rectangle(x, y, side, side),
                                              ImgPF.Format32bppArgb);
                square.RotateFlip(RotateFlipType.RotateNoneFlipY);

                BitmapData d = square.LockBits(new Rectangle(0, 0, side, side),
                                               ImageLockMode.ReadOnly,
                                               ImgPF.Format32bppArgb);

                GL.TexImage2D(TextureTarget.TextureCubeMapPositiveX + i, 0,
                              PixelInternalFormat.Rgba,
                              side, side, 0,
                              GlPF.Bgra, PixelType.UnsignedByte, d.Scan0);

                square.UnlockBits(d);
                orig.Dispose();
            }
            bitmaps.Clear();

            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter,
                            (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter,
                            (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS,
                            (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT,
                            (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR,
                            (int)TextureWrapMode.ClampToEdge);

            return tex;
        }

        // Cleanup
        public void Dispose()
        {
            if (!_ok) return;
            GL.DeleteVertexArray(_vao);
            GL.DeleteBuffer(_vbo);
            GL.DeleteProgram(_prog);
            GL.DeleteTexture(_cubeMap);
            _ok = false;
        }
    }
}
