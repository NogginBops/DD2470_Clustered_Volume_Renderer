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
        public static readonly List<WeakReference<Shader>> AllShaders = new List<WeakReference<Shader>>();

        public string Name;
        public int Handle;
        // Add hot reload information.

        public string? VertexPath;
        public string? FragmentPath;

        public string? ComputePath;
        
        public Shader(string name, int handle)
        {
            Name = name;
            Handle = handle;

            AllShaders.Add(new WeakReference<Shader>(this));
        }

        public static Shader CreateVertexFragment(string name, string vertexPath, string fragmentPath)
        {
            string vertexSource = File.ReadAllText(vertexPath);
            string fragmentSource = File.ReadAllText(fragmentPath);

            int vertex = CompileShader($"{name}_vertex", ShaderType.VertexShader, vertexSource);
            int fragment = CompileShader($"{name}_fragment", ShaderType.FragmentShader, fragmentSource);

            int program = LinkProgram(name, stackalloc int[2] { vertex, fragment });

            Shader shader = new Shader(name, program);
            shader.VertexPath = vertexPath;
            shader.FragmentPath = fragmentPath;
            return shader;
        }

        public static Shader CreateCompute(string name, string computePath)
        {
            string computeSource = File.ReadAllText(computePath);

            int computeShader = CompileShader($"{name}_compute", ShaderType.ComputeShader, computeSource);
            int program = LinkProgram(name, stackalloc int[1] { computeShader });

            Shader shader = new Shader(name, program);
            shader.ComputePath = computePath;
            return shader;
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
                Console.WriteLine($"Failed to link program '{name}': {log}");
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
                Console.WriteLine($"Failed to compile shader '{name}': {log}");
                // FIXME: Return error shader
                return 0;
            }

            return shader;
        }

        public static void RecompileAllShaders()
        {
            foreach (var shaderRef in AllShaders)
            {
                if (shaderRef.TryGetTarget(out Shader? shader))
                {
                    shader.RecompileShader();
                }
            }

            // Remove all garbage collected shaders.
            AllShaders.RemoveAll(wr => wr.TryGetTarget(out _) == false);
        }

        public void RecompileShader()
        {
            if (ComputePath == null)
            {
                // Vertex fragment shader
                int vert = CompileShader($"{Name}_vertex", ShaderType.VertexShader, File.ReadAllText(VertexPath!));
                int frag = CompileShader($"{Name}_fragment", ShaderType.FragmentShader, File.ReadAllText(FragmentPath!));

                int program = LinkProgram(Name, stackalloc int[2] { vert, frag });

                if (program != 0)
                {
                    Handle = program;
                }
                else
                {
                    // Set a default error shader...
                }
            }
            else
            {
                int comp = CompileShader($"{Name}_compute", ShaderType.ComputeShader, File.ReadAllText(ComputePath));
                int program = LinkProgram(Name, stackalloc int[1] { comp });

                if (program != 0)
                {
                    Handle = program;
                }
                else
                {
                    // Set error shader??
                }
            }
        }
    }
}
