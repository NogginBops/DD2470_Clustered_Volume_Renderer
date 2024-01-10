using OpenTK;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DD2470_Clustered_Volume_Renderer
{
    internal class CameraConfig
    {
        public string Name;

        public Vector3 CameraPosition;
        public Vector2 CameraRotation;

        public float Near, Far;
        public float Fov;

        public CameraConfig(string name)
        {
            Name = name;
        }

        public CameraConfig(string name, Camera camera)
        {
            Name = name;
            CameraPosition = camera.Transform.LocalPosition;
            CameraRotation = (camera.XAxisRotation, camera.YAxisRotation);

            Near = camera.NearPlane;
            Far = camera.FarPlane;
            Fov = camera.VerticalFov;
        }

        public static bool HasConfig(Camera camera, CameraConfig config)
        {
            if (camera.Transform.LocalPosition == config.CameraPosition &&
                camera.XAxisRotation == config.CameraRotation.X &&
                camera.YAxisRotation == config.CameraRotation.Y &&
                camera.NearPlane == config.Near &&
                camera.FarPlane == config.Far &&
                camera.VerticalFov == config.Fov)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public static void ApplyConfig(Camera camera, CameraConfig config)
        {
            camera.Transform.LocalPosition = config.CameraPosition;
            camera.XAxisRotation = config.CameraRotation.X;
            camera.YAxisRotation = config.CameraRotation.Y;
            camera.Transform.LocalRotation =
                        Quaternion.FromAxisAngle(Vector3.UnitY, camera.YAxisRotation) *
                        Quaternion.FromAxisAngle(Vector3.UnitX, camera.XAxisRotation);
            camera.NearPlane = config.Near;
            camera.FarPlane = config.Far;
            camera.VerticalFov = config.Fov;
        }

        public static void WriteConfigurations(string path, List<CameraConfig> configs)
        {
            using TextWriter writer = new StreamWriter(File.OpenWrite(path));
            foreach (CameraConfig config in configs)
            {
                writer.WriteLine($"#{config.Name}");
                writer.WriteLine($"{config.CameraPosition.X} {config.CameraPosition.Y} {config.CameraPosition.Z}");
                writer.WriteLine($"{config.CameraRotation.X} {config.CameraRotation.X}");
                writer.WriteLine($"{config.Near} {config.Far} {config.Fov}");
                writer.WriteLine();
            }
        }

        public static List<CameraConfig> ReadConfigurations(string path)
        {
            if (File.Exists(path) == false)
            {
                File.Create(path);
            }

            string[] lines = File.ReadAllLines(path);

            List<CameraConfig> configs = new List<CameraConfig>();
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.StartsWith('#'))
                {
                    string name = line.Substring(1);
                    float[] pos = lines[i + 1].Split(' ').Select(float.Parse).ToArray();
                    float[] rotation = lines[i + 2].Split(' ').Select(float.Parse).ToArray();
                    float[] nearFarFov = lines[i + 3].Split(' ').Select(float.Parse).ToArray();

                    configs.Add(new CameraConfig(name)
                    {
                        CameraPosition = new Vector3(pos[0], pos[1], pos[2]),
                        CameraRotation = new Vector2(rotation[0], rotation[1]),
                        Near = nearFarFov[0],
                        Far = nearFarFov[1],
                        Fov = nearFarFov[2],
                    });

                    i += 4;
                }
            }

            return configs;
        }
    }

    internal class LightConfig
    {
        public string Name;
        public List<PointLight> Lights;

        public LightConfig(string name, List<PointLight> lights)
        {
            Name = name;
            Lights = lights;
        }

        public static void WriteConfigurations(string path, List<LightConfig> configs)
        {
            using TextWriter writer = new StreamWriter(File.OpenWrite(path));
            foreach (LightConfig config in configs)
            {
                writer.WriteLine($"#{config.Name}");
                foreach (var light in config.Lights)
                {
                    writer.WriteLine($"{light.Position.X} {light.Position.Y} {light.Position.Z} {float.Sqrt(light.SquareRadius)} {light.Color.X} {light.Color.Y} {light.Color.Z}");
                }
                writer.WriteLine();
            }
        }

        public static List<LightConfig> ReadConfigurations(string path)
        {
            if (File.Exists(path) == false)
            {
                File.Create(path);
            }

            string[] lines = File.ReadAllLines(path);

            List<LightConfig> configs = new List<LightConfig>();
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.StartsWith('#'))
                {
                    string name = line.Substring(1);
                    List<PointLight> lights = new List<PointLight>();
                    int offset = 1;
                    while (string.IsNullOrEmpty(lines[i + offset]) == false)
                    {
                        float[] posRadColor = lines[i + offset].Split(' ').Select(float.Parse).ToArray();

                        Vector3 pos = (posRadColor[0], posRadColor[1], posRadColor[2]);
                        float radius = posRadColor[3];
                        Color4 color = new Color4(posRadColor[4], posRadColor[5], posRadColor[6], 1.0f);
                        lights.Add(new PointLight(pos, radius, color, 1.0f));

                        offset++;
                    }
                    configs.Add(new LightConfig(name, lights));
                    i += offset;
                }
            }

            return configs;
        }
    }
}
