using OpenTK.Graphics.OpenGL4;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DD2470_Clustered_Volume_Renderer
{
    internal class Shader
    {
        public int Handle;
        // Add hot reload information.

        public Shader(int handle)
        {
            Handle = handle;
        }


        public static Shader? currentShader;
        public static void UseShader(Shader? shader)
        {
            // FIXME: Maybe compare the handle itself?
            if (currentShader != shader)
            {
                GL.UseProgram(shader?.Handle ?? 0);
            }
        }

        public static Shader CreateVertexFragment(string name, string vertexPath, string fragmentPath)
        {
            string vertexSource = File.ReadAllText(vertexPath);
            string fragmentSource = File.ReadAllText(fragmentPath);

            int vertex = CompileShader($"{name}_vertex", ShaderType.VertexShader, vertexSource);
            int fragment = CompileShader($"{name}_fragment", ShaderType.FragmentShader, fragmentSource);

            int program = LinkProgram(name, stackalloc int[2] { vertex, fragment });

            return new Shader(program);
        }

        public static Shader CreateCompute(string name, string computeSource)
        {
            int shader = CompileShader($"{name}_compute", ShaderType.ComputeShader, computeSource);
            int program = LinkProgram(name, stackalloc int[1] { shader });

            return new Shader(program);
        }

        public static int LinkProgram(string name, ReadOnlySpan<int> shaders)
        {
            int program = GL.CreateProgram();
            GL.ObjectLabel(ObjectLabelIdentifier.Program, program, -1, name);

            for (int i = 0; i < shaders.Length; i++)
            {
                GL.AttachShader(program, shaders[i]);
            }

            GL.LinkProgram(program);

            for (int i = 0; i < shaders.Length; i++)
            {
                GL.DetachShader(program, shaders[i]);
                GL.DeleteShader(shaders[i]);
            }

            GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int success);
            if (success == 0)
            {
                string log = GL.GetProgramInfoLog(program);
                Console.WriteLine($"Failed to link program: {log}");
                return 0;
            }

            return program;
        }

        public static int CompileShader(string name, ShaderType type, string source)
        {
            int shader = GL.CreateShader(type);
            GL.ObjectLabel(ObjectLabelIdentifier.Shader, shader, -1, name);
            
            GL.ShaderSource(shader, source);

            GL.CompileShader(shader);

            GL.GetShader(shader, ShaderParameter.CompileStatus, out int success);
            if (success == 0)
            {
                string log = GL.GetShaderInfoLog(shader);
                Console.WriteLine($"Failed to compile shader: {log}");
                // FIXME: Return error shader
                return 0;
            }

            return shader;
        }
    }
}
