// skybox
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using GlPF = OpenTK.Graphics.OpenGL4.PixelFormat;
using ImgPF = System.Drawing.Imaging.PixelFormat;

namespace MyLavaRunner
{
    internal sealed class SkyBox : IDisposable
    {
        int _vao,_vbo,_prog,_tex,_cnt;
        bool _ok;

        // init
        public void Load(string dir="Resources/Textures/skybox/")
        {
            if(_ok)return;

            // sphere
            BuildSphere(256,128,out float[] v,out _cnt);
            _vao=GL.GenVertexArray();
            _vbo=GL.GenBuffer();
            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer,_vbo);
            GL.BufferData(BufferTarget.ArrayBuffer,v.Length*sizeof(float),v,BufferUsageHint.StaticDraw);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0,3,VertexAttribPointerType.Float,false,3*sizeof(float),0);

            // shaders
            const string vs=
                "#version 330 core\nlayout(location=0)in vec3 p;"
              + "out vec3 t;uniform mat4 V,P;"
              + "void main(){t=normalize(p);vec4 q=P*V*vec4(p,1);gl_Position=q.xyww;}";
            const string fs=
                "#version 330 core\nin vec3 t;out vec4 c;uniform samplerCube S;"
              + "void main(){c=texture(S,t);}";

            _prog=Compile(vs,fs);
            GL.UseProgram(_prog);
            GL.Uniform1(GL.GetUniformLocation(_prog,"S"),0);

            // cubemap
            string[] f={"right.jpg","left.jpg","top.jpg","bottom.jpg","front.jpg","back.jpg"};
            _tex=GL.GenTexture();
            GL.BindTexture(TextureTarget.TextureCubeMap,_tex);
            for(int i=0;i<6;i++)
            {
                using var bmp=new Bitmap(dir+f[i]);
                var d=bmp.LockBits(new Rectangle(0,0,bmp.Width,bmp.Height),ImageLockMode.ReadOnly,ImgPF.Format32bppArgb);
                GL.TexImage2D(TextureTarget.TextureCubeMapPositiveX+i,0,PixelInternalFormat.Rgba,
                    bmp.Width,bmp.Height,0,GlPF.Bgra,PixelType.UnsignedByte,d.Scan0);
                bmp.UnlockBits(d);
            }
            GL.TexParameter(TextureTarget.TextureCubeMap,TextureParameterName.TextureMinFilter,(int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.TextureCubeMap,TextureParameterName.TextureMagFilter,(int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.TextureCubeMap,TextureParameterName.TextureWrapS,(int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.TextureCubeMap,TextureParameterName.TextureWrapT,(int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.TextureCubeMap,TextureParameterName.TextureWrapR,(int)TextureWrapMode.ClampToEdge);

            _ok=true;
        }

        // draw
        public void Draw(Matrix4 view,Matrix4 proj)
        {
            if(!_ok)return;
            GL.Disable(EnableCap.CullFace);
            GL.DepthMask(false);
            GL.DepthFunc(DepthFunction.Lequal);

            Matrix4 v=view; v.Row3.Xyz=Vector3.Zero;
            GL.UseProgram(_prog);
            GL.UniformMatrix4(GL.GetUniformLocation(_prog,"V"),false,ref v);
            GL.UniformMatrix4(GL.GetUniformLocation(_prog,"P"),false,ref proj);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.TextureCubeMap,_tex);
            GL.BindVertexArray(_vao);
            GL.DrawArrays(PrimitiveType.Triangles,0,_cnt);
            GL.BindVertexArray(0);

            GL.DepthMask(true);
            GL.DepthFunc(DepthFunction.Less);
        }

        // helpers
        static void BuildSphere(int lon,int lat,out float[] verts,out int cnt)
        {
            var list=new List<float>();
            for(int y=0;y<lat;y++)
            {
                float v0=y/(float)lat,v1=(y+1f)/lat;
                float p0=MathF.PI*v0,p1=MathF.PI*v1;
                for(int x=0;x<=lon;x++)
                {
                    float u=x/(float)lon,t=2*MathF.PI*u;
                    Vector3 a=Dir(t,p0),b=Dir(t,p1);
                    list.AddRange(new[]{a.X,a.Y,a.Z});
                    list.AddRange(new[]{b.X,b.Y,b.Z});
                }
            }
            verts=list.ToArray(); cnt=verts.Length/3;
            static Vector3 Dir(float t,float p)=>new(
                -MathF.Sin(p)*MathF.Cos(t),
                 MathF.Cos(p),
                 MathF.Sin(p)*MathF.Sin(t));
        }
        static int Compile(string v,string f)
        {
            int V=GL.CreateShader(ShaderType.VertexShader);GL.ShaderSource(V,v);GL.CompileShader(V);
            int F=GL.CreateShader(ShaderType.FragmentShader);GL.ShaderSource(F,f);GL.CompileShader(F);
            int P=GL.CreateProgram();GL.AttachShader(P,V);GL.AttachShader(P,F);GL.LinkProgram(P);
            GL.DeleteShader(V);GL.DeleteShader(F);return P;
        }
        public void Dispose()
        {
            if(!_ok)return;
            GL.DeleteBuffer(_vbo);GL.DeleteVertexArray(_vao);
            GL.DeleteProgram(_prog);GL.DeleteTexture(_tex);
        }
    }
}
