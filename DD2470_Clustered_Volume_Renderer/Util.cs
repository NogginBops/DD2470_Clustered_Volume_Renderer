using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DD2470_Clustered_Volume_Renderer
{
    internal static class Util
    {
        public const float D2R = MathF.PI / 180f;
        public const float R2D = 180f / MathF.PI;


        public static Vector3 NextVector3(this Random random, Vector3 min, Vector3 max)
        {
            Vector3 vec;
            vec.X = random.NextSingle();
            vec.Y = random.NextSingle();
            vec.Z = random.NextSingle();
            return Vector3.Lerp(min, max, vec);
        }

        public static Color4 NextColor4Hue(this Random random, float saturation, float value)
        {
            float hue = random.NextSingle();
            return Color4.FromHsv(new Vector4(hue, saturation, value, 1.0f));
        }
    }
}
