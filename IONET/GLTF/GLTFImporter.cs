using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using IONET.Collada.Core.Scene;
using IONET.Core;
using IONET.Core.Model;
using IONET.Core.Skeleton;
using System.Numerics;
using IONET.Collada.Helpers;
using IONET.Collada.Core.Geometry;
using IONET.Collada.Enums;
using IONET.Collada.FX.Materials;
using IONET.Collada.FX.Rendering;
using IONET.Collada.FX.Profiles.COMMON;
using System.Xml;
using SharpGLTF.Schema2;
using IONET.Collada.Core.Animation;
using IONET.Core.Animation;
using System.Runtime.Serialization.Json;

namespace IONET.GLTF
{
    public class GLTFImporter : ISceneLoader
    {
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string Name()
        {
            return "GLTF";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string[] GetExtensions()
        {
            return new string[] { ".gltf", ".glb" };
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public bool Verify(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLower();
            return ext.Equals(".gltf") || ext.Equals(".glb");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public IOScene GetScene(string filePath, ImportSettings settings)
        {
            // generate a new scene
            IOScene scene = new IOScene();
            IOModel iomodel = new IOModel();
            iomodel.Name = Path.GetFileNameWithoutExtension(filePath);
            scene.Models.Add(iomodel);

            var model = SharpGLTF.Schema2.ModelRoot.Load(filePath);
            foreach (var node in model.LogicalScenes[0].VisualChildren)
                ProcessNodes(iomodel, node, null, Matrix4x4.Identity);

            //Animation handling
            foreach (var anim in model.LogicalAnimations)
            {
                float rate = 24;
                float frameCount = anim.Duration * rate;

                IOAnimation ioanim = new IOAnimation();
                ioanim.Name = anim.Name;
                scene.Animations.Add(ioanim);

                foreach (var channel in anim.Channels)
                {
                    if (channel.TargetNode == null)
                        continue;

                    IOAnimation group = ioanim.Groups.FirstOrDefault(x => x.Name == channel.TargetNode.Name);
                    if (group == null)
                    {
                        group = new IOAnimation();
                        group.Name = channel.TargetNode.Name;
                        ioanim.Groups.Add(group);
                    }             

                    var trans = channel.GetTranslationSampler();
                    var rotation = channel.GetRotationSampler(); //in quat
                    var scale = channel.GetScaleSampler();
                    var morph = channel.GetMorphSampler();

                    void CreateVec4Track(IAnimationSampler<Quaternion> quat, IOAnimationTrackType type)
                    {
                        IOAnimationTrack X = new IOAnimationTrack(type + 0);
                        IOAnimationTrack Y = new IOAnimationTrack(type + 1);
                        IOAnimationTrack Z = new IOAnimationTrack(type + 2);
                        IOAnimationTrack W = new IOAnimationTrack(type + 3);
                        group.Tracks.Add(X);
                        group.Tracks.Add(Y);
                        group.Tracks.Add(Z);
                        group.Tracks.Add(W);

                        switch (quat.InterpolationMode)
                        {
                            case AnimationInterpolationMode.CUBICSPLINE:
                                foreach (var linear in quat.GetCubicKeys())
                                {
                                    var kf = linear.Value;
                                    X.InsertKeyframe(linear.Key * rate, kf.Value.X, kf.TangentIn.X, kf.TangentOut.X);
                                    Y.InsertKeyframe(linear.Key * rate, kf.Value.Y, kf.TangentIn.Y, kf.TangentOut.Y);
                                    Z.InsertKeyframe(linear.Key * rate, kf.Value.Z, kf.TangentIn.Z, kf.TangentOut.Z);
                                    W.InsertKeyframe(linear.Key * rate, kf.Value.W, kf.TangentIn.W, kf.TangentOut.W);
                                }
                                break;
                            default:
                                foreach (var linear in quat.GetLinearKeys())
                                {
                                    X.InsertKeyframe(linear.Key * rate, linear.Value.X);
                                    Y.InsertKeyframe(linear.Key * rate, linear.Value.Y);
                                    Z.InsertKeyframe(linear.Key * rate, linear.Value.Z);
                                    W.InsertKeyframe(linear.Key * rate, linear.Value.W);
                                }
                                break;
                        }
                    }

                    void CreateVec3Track(IAnimationSampler<Vector3> vec3, IOAnimationTrackType type)
                    {
                        IOAnimationTrack X = new IOAnimationTrack(type + 0);
                        IOAnimationTrack Y = new IOAnimationTrack(type + 1);
                        IOAnimationTrack Z = new IOAnimationTrack(type + 2);
                        group.Tracks.Add(X);
                        group.Tracks.Add(Y);
                        group.Tracks.Add(Z);

                        switch (vec3.InterpolationMode)
                        {
                            case AnimationInterpolationMode.CUBICSPLINE:
                                foreach (var linear in vec3.GetCubicKeys())
                                {
                                    var kf = linear.Value;
                                    X.InsertKeyframe(linear.Key * rate, kf.Value.X, MathF.Atan(kf.TangentIn.X), MathF.Atan(kf.TangentOut.X));
                                    Y.InsertKeyframe(linear.Key * rate, kf.Value.Y, MathF.Atan(kf.TangentIn.Y), MathF.Atan(kf.TangentOut.Y));
                                    Z.InsertKeyframe(linear.Key * rate, kf.Value.Z, MathF.Atan(kf.TangentIn.Z), MathF.Atan(kf.TangentOut.Z));
                                }
                                break;
                            default:
                                foreach (var linear in vec3.GetLinearKeys())
                                {
                                    X.InsertKeyframe(linear.Key * rate, linear.Value.X);
                                    Y.InsertKeyframe(linear.Key * rate, linear.Value.Y);
                                    Z.InsertKeyframe(linear.Key * rate, linear.Value.Z);
                                }
                                break;
                        }
                    }

                    void CreateFloatArrayTrack(IAnimationSampler<float[]> v, IOAnimationTrackType type)
                    {
                        IOAnimationTrack X = new IOAnimationTrack(type);
                        group.Tracks.Add(X);

                        switch (v.InterpolationMode)
                        {
                            case AnimationInterpolationMode.CUBICSPLINE:
                                foreach (var linear in v.GetCubicKeys())
                                {
                                    X.InsertKeyframe(linear.Key * rate, linear.Value.Value, linear.Value.TangentIn, linear.Value.TangentOut);
                                }
                                break;
                            default:
                                foreach (var linear in v.GetLinearKeys())
                                {
                                    X.InsertKeyframe(linear.Key * rate, linear.Value);
                                }
                                break;
                        }
                    }

                    if (trans != null)
                        CreateVec3Track(trans, IOAnimationTrackType.PositionX);
                    if (scale != null)
                        CreateVec3Track(scale, IOAnimationTrackType.ScaleX);
                    if (rotation != null)
                        CreateVec4Track(rotation, IOAnimationTrackType.QuatX);
                    if (morph != null)
                        CreateFloatArrayTrack(morph, IOAnimationTrackType.MorphWeight);
                }
            }

            foreach (var mat in model.LogicalMaterials)
            {
                IOMaterial iomaterial = new IOMaterial();
                iomaterial.Name = mat.Name;
                scene.Materials.Add(iomaterial);

                //Texture map handling
                foreach (var channel in mat.Channels)
                {
                    //No texture, skip
                    if (channel.Texture == null || channel.Texture.PrimaryImage == null)
                        continue;

                    //texture name is empty, skip
                    string texName = channel.Texture.PrimaryImage.Name;
                    if (string.IsNullOrEmpty(texName))
                        continue;

                    switch (channel.Key)
                    {
                        case "BaseColor": iomaterial.DiffuseMap = ConvertTextureMap(channel.Texture, channel.TextureSampler); break;
                        case "Normal": iomaterial.NormalMap = ConvertTextureMap(channel.Texture, channel.TextureSampler); break;
                        case "Emissive": iomaterial.EmissionMap = ConvertTextureMap(channel.Texture, channel.TextureSampler); break;
                    }
                }
            }


            // done
            return scene;
        }

        private IOTexture ConvertTextureMap(SharpGLTF.Schema2.Texture texture, SharpGLTF.Schema2.TextureSampler sampler)
        {
            WrapMode ConvertWrap(TextureWrapMode mode)
            {
                switch (mode)
                {
                    case TextureWrapMode.REPEAT: return WrapMode.REPEAT;
                    case TextureWrapMode.MIRRORED_REPEAT: return WrapMode.MIRROR;
                    case TextureWrapMode.CLAMP_TO_EDGE: return WrapMode.CLAMP;
                }
                return WrapMode.REPEAT;
            }

            string path = texture.PrimaryImage.AlternateWriteFileName;

            return new IOTexture()
            {
                Name = texture.PrimaryImage.Name,
                FilePath = path == null ? "" : path,
                WrapS = ConvertWrap(texture.Sampler.WrapS),
                WrapT = ConvertWrap(texture.Sampler.WrapT),
            };
        }

        private void ProcessNodes(IOModel iomodel, SharpGLTF.Schema2.Node node, IOBone boneParent, Matrix4x4 parentMatrix)
        {
            var worldTransform = node.LocalMatrix * parentMatrix;

            if (node.Mesh != null)
                iomodel.Meshes.Add(CreateMesh(node.Mesh, node.Skin, node.WorldMatrix));

            //Add bone if skinning type is used or parent bone exists
            IOBone iobone = null;
            if (node.IsSkinSkeleton || node.IsSkinJoint || boneParent != null)
            {
                iobone = new IOBone();
                iobone.Name = node.Name;
                iobone.LocalTransform = node.LocalMatrix;
                if (boneParent != null)
                    boneParent.AddChild(iobone);
                else
                {
                    //apply parent matrix to apply prior node transforms to the skeleton root
                    iobone.LocalTransform = node.LocalMatrix * parentMatrix;

                    iomodel.Skeleton.RootBones.Add(iobone);
                }
            }

            foreach (var child in node.VisualChildren)
                ProcessNodes(iomodel, child, iobone, worldTransform);
        }

        private IOMesh CreateMesh(SharpGLTF.Schema2.Mesh mesh, SharpGLTF.Schema2.Skin skin, Matrix4x4 worldTransform)
        {
            IOMesh iomesh = new IOMesh();
            iomesh.Name = mesh.Name;
            foreach (var prim in mesh.Primitives)
            {
                IOPolygon iopoly = new IOPolygon();
                iomesh.Polygons.Add(iopoly);

                if (prim.Material != null)
                    iopoly.MaterialName = prim.Material.Name;

                foreach (var tri in prim.GetTriangleIndices())
                {
                    iopoly.Indicies.Add(tri.A);
                    iopoly.Indicies.Add(tri.B);
                    iopoly.Indicies.Add(tri.C);
                }

                //tex coord channel list
                List<IList<Vector2>> texCoords = new List<IList<Vector2>>();
                foreach (var accessor in prim.VertexAccessors)
                {
                    if (accessor.Key.StartsWith("TEXCOORD_"))
                        texCoords.Add(accessor.Value.AsVector2Array());
                }

                //color channel list
                List<IList<Vector4>> colorList = new List<IList<Vector4>>();
                foreach (var accessor in prim.VertexAccessors)
                {
                    if (accessor.Key.StartsWith("COLOR_"))
                        colorList.Add(accessor.Value.AsColorArray());
                }

                //weight list
                List<IList<Vector4>> weightList = new List<IList<Vector4>>();
                foreach (var accessor in prim.VertexAccessors)
                {
                    if (accessor.Key.StartsWith("WEIGHTS_"))
                        weightList.Add(accessor.Value.AsVector4Array());
                }

                //bone index list
                List<IList<Vector4>> boneIndexList = new List<IList<Vector4>>();
                foreach (var accessor in prim.VertexAccessors)
                {
                    if (accessor.Key.StartsWith("JOINTS_"))
                        boneIndexList.Add(accessor.Value.AsVector4Array());
                }

                //positions
                var pos = prim.GetVertexAccessor("POSITION").AsVector3Array();

                //normals
                var nrm = prim.GetVertexAccessor("NORMAL")?.AsVector3Array();
                iomesh.HasNormals = nrm?.Count > 0;

                //tangents
                var tangent = prim.GetVertexAccessor("TANGENT")?.AsVector4Array();
                iomesh.HasTangents = tangent?.Count > 0;

                //Init a vertex list
                IOVertex[] vertices = new IOVertex[pos.Count];
                for (int i = 0; i < vertices.Length; i++)
                {
                    vertices[i] = new IOVertex();

                    //positions
                    vertices[i].Position = pos[i];

                    //normals
                    if (nrm?.Count > 0) vertices[i].Normal = nrm[i];

                    //tex coord channel list
                    for (int j = 0; j < texCoords?.Count; j++)
                        vertices[i].SetUV(texCoords[j][i].X, texCoords[j][i].Y, j);

                    //vertex color channel list
                    for (int j = 0; j < colorList?.Count; j++)
                        vertices[i].SetColor(
                            colorList[j][i].X, colorList[j][i].Y, colorList[j][i].Z, colorList[j][i].W, j);

                    //tangents
                    if (tangent?.Count > 0) 
                        vertices[i].Tangent = new Vector3(tangent[i].X, tangent[i].Y, tangent[i].Z);

                    if (weightList.Count > 0) //bone indices + weights
                    {
                        for (int b = 0; b < boneIndexList.Count; b++)
                        {
                            var bones4 = boneIndexList[b][i];
                            var weights4 = weightList[b][i];

                            float[] weights = new float[4] { weights4.X, weights4.Y, weights4.Z, weights4.W };
                            float[] indices = new float[4] { bones4.X, bones4.Y, bones4.Z, bones4.W };

                            for (int j = 0; j < 4; j++)
                            {
                                if (weights[j] == 0)
                                    continue;

                                var joint = skin.GetJoint((int)indices[j]);
                                vertices[i].Envelope.Weights.Add(new IOBoneWeight()
                                {
                                    BoneName = joint.Joint.Name,
                                    Weight = weights[j],
                                });
                            }
                        }
                    }
                    else if (boneIndexList.Count > 0) //bone indices rigid, no weights
                    {
                        var bones = boneIndexList[0][i];
                        var joint = skin.GetJoint((int)bones.X);
                        //only 1 bone
                        vertices[i].Envelope.Weights.Add(new IOBoneWeight()
                        {
                            BoneName = joint.Joint.Name,
                            Weight = 1f,
                        });
                    }
                }
                iomesh.Vertices.AddRange(vertices);

                //transform mesh
            //    iomesh.TransformVertices(worldTransform);
            }
            return iomesh;
        }
    }
}
