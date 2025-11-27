using Assimp;
using Assimp.Unmanaged;
using IONET.Collada.Core.Transform;
using System;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using SN = System.Numerics;

namespace IONET.Assimp
{
    public static class AssimpHelper
    {


        public static bool IsRuntimePresent()
        {
            string platform = "";
            string fileName = "";

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                platform = "linux";
                fileName = "assimp.so";
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
                RuntimeInformation.OSDescription.Contains("Microsoft Windows"))
            {
                platform = "win";
                fileName = "assimp.dll";
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                platform = "osx";
                fileName = "libassimp.dylib";
            }

            if (!File.Exists(Path.Combine(AppContext.BaseDirectory, "runtimes", $"{platform}-{GetRIDArch()}", "native", fileName)))
                return false;

            return true;
        }

        static string GetRIDArch()
        {
            switch (RuntimeInformation.ProcessArchitecture)
            {
                case Architecture.Arm:
                    return "arm";
                case Architecture.Arm64:
                    return "arm64";
                case Architecture.X86:
                    return "x86";
                case Architecture.X64:
                default:
                    return "x64";
            }
        }

        public static Vector3 ToEuler(this Quaternion q)
        {
            Matrix4x4 mat = Matrix4x4.CreateFromQuaternion(q);
            float x, y, z;
            y = (float)Math.Asin(Math.Clamp(mat.M13, -1, 1));

            if (Math.Abs(mat.M13) < 0.99999)
            {
                x = (float)Math.Atan2(-mat.M23, mat.M33);
                z = (float)Math.Atan2(-mat.M12, mat.M11);
            }
            else
            {
                x = (float)Math.Atan2(mat.M32, mat.M22);
                z = 0;
            }
            return new Vector3(x, y, z) * -1;
        }

        public static Matrix4x4 ToNumerics(this SN.Matrix4x4 matIn)
        {
            //Assimp matrices are column vector, so X,Y,Z axes are columns 1-3 and 4th column is translation.
            //Columns => Rows to make it compatible with numerics
            return new System.Numerics.Matrix4x4(matIn.M11, matIn.M21, matIn.M31, matIn.M41, //X
                                                   matIn.M12, matIn.M22, matIn.M32, matIn.M42, //Y
                                                   matIn.M13, matIn.M23, matIn.M33, matIn.M43, //Z
                                                   matIn.M14, matIn.M24, matIn.M34, matIn.M44); //Translation
        }

        public static Matrix4x4 FromNumerics(this SN.Matrix4x4 matIn)
        {
            //Numerics matrix are row vector, so X,Y,Z axes are rows 1-3 and 4th row is translation.
            //Rows => Columns to make it compatible with assimp

            SN.Matrix4x4 matOut;

            //X
            matOut.M11 = matIn.M11;
            matOut.M21 = matIn.M12;
            matOut.M31 = matIn.M13;
            matOut.M41 = matIn.M14;

            //Y
            matOut.M12 = matIn.M21;
            matOut.M22 = matIn.M22;
            matOut.M32 = matIn.M23;
            matOut.M42 = matIn.M24;

            //Z
            matOut.M13 = matIn.M31;
            matOut.M23 = matIn.M32;
            matOut.M33 = matIn.M33;
            matOut.M43 = matIn.M34;

            //Translation
            matOut.M14 = matIn.M41;
            matOut.M24 = matIn.M42;
            matOut.M34 = matIn.M43;
            matOut.M44 = matIn.M44;

            return matOut;
        }
    }
}
