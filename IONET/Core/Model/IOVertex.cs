using IONET.Core.Skeleton;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace IONET.Core.Model
{
    public class IOVertex
    {
        public Vector3 Position = Vector3.Zero;
        public Vector3 Normal = Vector3.UnitX;

        public Vector3 Tangent = Vector3.UnitX;
        public Vector3 Binormal = Vector3.UnitX;

        public List<Vector2> UVs { get; internal set; } = new List<Vector2>();
        public List<Vector4> Colors { get; internal set; } = new List<Vector4>();

        public IOEnvelope Envelope { get; internal set; } = new IOEnvelope();

        /// <summary>
        /// Transforms vertic by matrix
        /// </summary>
        /// <param name="transform"></param>
        public void Transform(Matrix4x4 transform)
        {
            Position = Vector3.Transform(Position, transform);
            Normal = Vector3.Normalize(Vector3.TransformNormal(Normal, transform));
            Tangent = Vector3.Normalize(Vector3.TransformNormal(Tangent, transform));
            Binormal = Vector3.Normalize(Vector3.TransformNormal(Binormal, transform));
        }

        /// <summary>
        /// 
        /// </summary>
        public void SetUV(float s, float t, int set = 0)
        {
            while (UVs.Count <= set)
                UVs.Add(new Vector2());

            UVs[set] = new Vector2(s, t);
        }

        /// <summary>
        /// 
        /// </summary>
        public void SetColor(float r, float g, float b, float a, int set = 0)
        {
            while (Colors.Count <= set)
                Colors.Add(new Vector4());

            Colors[set] = new Vector4(r, g, b, a);
        }

        /// <summary>
        /// Resets inverse binds to the skeleton
        /// </summary>
        public void ResetEnvelope(IOSkeleton skeleton)
        {
            if (!Envelope.UseBindMatrix || Envelope == null || Envelope.Weights.Count == 0)
                return;

            var pos = Vector3.Zero;
            var nrm = Vector3.Zero;
            var tan = Vector3.Zero;
            var bit = Vector3.Zero;
            foreach (var bw in Envelope.Weights)
            {
                var bone = skeleton.GetBoneByName(bw.BoneName);

                if (bone == null)
                    throw new KeyNotFoundException($"Bone {bw.BoneName} not found");

                // calculate bind matrix
                var bind = bw.BindMatrix * bone.WorldTransform;

                // rebind
                pos += Vector3.Transform(Position, bind) * bw.Weight;
                nrm += Vector3.TransformNormal(Normal, bind) * bw.Weight;
                tan += Vector3.TransformNormal(Tangent, bind) * bw.Weight;
                bit += Vector3.TransformNormal(Binormal, bind) * bw.Weight;

                // set new bind matrix
                Matrix4x4.Invert(bone.WorldTransform, out Matrix4x4 inv);
                bw.BindMatrix = inv;
            }
            Position = pos;
            Normal = Vector3.Normalize(nrm);
            Tangent = Vector3.Normalize(tan);
            Binormal = Vector3.Normalize(bit);

            Envelope.UseBindMatrix = false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            if (!(obj is IOVertex vert))
                return false;

            if (Position != vert.Position || Normal != vert.Normal || Tangent != vert.Tangent || Binormal != vert.Binormal)
                return false;

            if (UVs.Count != vert.UVs.Count || Colors.Count != vert.Colors.Count)
                return false;

            for (int i = 0; i < UVs.Count; i++)
                if (UVs[i] != vert.UVs[i])
                    return false;

            for (int i = 0; i < Colors.Count; i++)
                if (Colors[i] != vert.Colors[i])
                    return false;

            if (Envelope.UseBindMatrix != vert.Envelope.UseBindMatrix || Envelope.Weights.Count != vert.Envelope.Weights.Count)
                return false;

            for (int i = 0; i < Envelope.Weights.Count; i++)
            {
                var w1 = Envelope.Weights[i];
                var w2 = vert.Envelope.Weights[i];
                if (w1.BoneName != w2.BoneName || w1.Weight != w2.Weight || w1.BindMatrix != w2.BindMatrix)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Position);
            hash.Add(Normal);
            hash.Add(Tangent);
            hash.Add(Binormal);

            foreach (var uv in UVs)
                hash.Add(uv);

            foreach (var color in Colors)
                hash.Add(color);

            hash.Add(Envelope.UseBindMatrix);
            foreach (var weight in Envelope.Weights)
            {
                hash.Add(weight.BoneName);
                hash.Add(weight.Weight);
                hash.Add(weight.BindMatrix);  // Matrix4x4 has GetHashCode
            }

            return hash.ToHashCode();
        }
    }
}
