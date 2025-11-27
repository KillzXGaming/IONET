using IONET.Core.Model;
using IONET.Core;
using IONET.Fbx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Assimp;
using IONET.Core.Skeleton;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using System.Text.Json;
using IONET.Core.Animation;

namespace IONET.Assimp
{
    internal class AssimpImport : ISceneLoader
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public IOScene GetScene(string filePath, ImportSettings settings)
        {
            PostProcessSteps postProcess = PostProcessSteps.Triangulate;
            postProcess |= PostProcessSteps.OptimizeMeshes;
            postProcess |= PostProcessSteps.CalculateTangentSpace;

            IOScene ioscene = new IOScene();
            IOModel iomodel = new IOModel();
            ioscene.Models.Add(iomodel);

            using AssimpContext assimpContext = new AssimpContext();

            var scene = assimpContext.ImportFile(filePath, postProcess);
            scene.Metadata.Clear();

            LoadMaterials(ioscene, scene);
            LoadNodes(ioscene, iomodel, scene);

            foreach (var anim in scene.Animations)
                ioscene.Animations.Add(LoadAnimation(anim));

            ioscene.LoadSkeletonFromNodes(iomodel);

            return ioscene;
        }

        private void LoadMaterials(IOScene ioscene, Scene scene)
        {
            WrapMode ConvertWrap(TextureWrapMode mode)
            {
                switch (mode)
                {
                    case TextureWrapMode.Wrap: return WrapMode.REPEAT;
                    case TextureWrapMode.Mirror: return WrapMode.MIRROR;
                    case TextureWrapMode.Clamp: return WrapMode.CLAMP;
                }
                return WrapMode.REPEAT;
            }

            IOTexture ConvertTexture(TextureSlot slot, bool hasTexture)
            {
                if (!hasTexture) return null;

                return new IOTexture()
                {
                    FilePath = slot.FilePath,
                    UVChannel = slot.UVIndex,
                    WrapS = ConvertWrap(slot.WrapModeU),
                    WrapT = ConvertWrap(slot.WrapModeV),
                    Name = Path.GetFileNameWithoutExtension(slot.FilePath),
                };
            }

            foreach (var material in scene.Materials)
            {
                ioscene.Materials.Add(new IOMaterial()
                {
                    Name = material.Name,   
                    DiffuseMap = ConvertTexture(material.TextureDiffuse, material.HasTextureDiffuse),
                    NormalMap = ConvertTexture(material.TextureNormal, material.HasTextureNormal),
                    EmissionMap = ConvertTexture(material.TextureEmissive, material.HasTextureEmissive),
                    AmbientMap = ConvertTexture(material.TextureAmbient, material.HasTextureAmbient),
                    AmbientOcclusionMap = ConvertTexture(material.TextureAmbientOcclusion, material.HasTextureAmbientOcclusion),
                    ReflectiveMap = ConvertTexture(material.TextureReflection, material.HasTextureReflection),
                    SpecularMap = ConvertTexture(material.TextureSpecular, material.HasTextureSpecular),
                    DiffuseColor = material.ColorDiffuse,
                    SpecularColor = material.ColorSpecular,
                    EmissionColor = material.ColorEmissive,
                    Alpha = material.TransparencyFactor,
                    Shininess = material.Shininess,
                });
            }
        }

        private void LoadNodes(IOScene ioscene, IOModel iomodel, Scene assimpScene)
        {
            foreach (var child in assimpScene.RootNode.Children)
                ioscene.Nodes.Add(ConvertNode(iomodel, assimpScene, child, Matrix4x4.Identity)); 
        }

        private IONode ConvertNode(IOModel iomodel, Scene assimpScene, Node assimpNode, Matrix4x4 parentTransform)
        {
            var ionode = new IONode();

            ionode.Name = assimpNode.Name;
            ionode.LocalTransform = assimpNode.Transform.ToNumerics();
            // Joint if node has meshes with no children attached
            ionode.IsJoint = !(assimpNode.HasMeshes && assimpNode.ChildCount == 0);

             var worldTansform = ionode.LocalTransform * parentTransform;

            if (assimpNode.HasMeshes)
            {
                for (int i = 0; i < assimpNode.MeshCount; i++)
                    iomodel.Meshes.Add(LoadMesh(assimpScene,
                        assimpScene.Meshes[assimpNode.MeshIndices[i]], worldTansform));

                ionode.Mesh = iomodel.Meshes[0];
            }

            foreach (var node in assimpNode.Children)
                ionode.AddChild(ConvertNode(iomodel, assimpScene, node, worldTansform));

            return ionode;
        }

        private IOMesh LoadMesh(Scene scene, Mesh assimpMesh, Matrix4x4 worldTansform)
        {
            IOMesh iomesh = new IOMesh()
            {
                Name = assimpMesh.Name,
            };

            IOEnvelope[] envelopes = new IOEnvelope[assimpMesh.VertexCount];
            for (int i = 0; i < assimpMesh.VertexCount; i++)
                envelopes[i] = new IOEnvelope();

            for (int j = 0; j < assimpMesh.BoneCount; j++)
            {
                var bone = assimpMesh.Bones[j];
                foreach (var w in bone.VertexWeights)
                {
                    envelopes[w.VertexID].Weights.Add(new IOBoneWeight()
                    {
                        BoneName = bone.Name,
                        Weight = w.Weight, 
                    });
                }
            }

            Console.WriteLine($"{iomesh.Name} {assimpMesh.BoneCount}");

            for (int i = 0; i < assimpMesh.VertexCount; i++)
            {
                IOVertex iovertex = new IOVertex()
                {
                    Position = assimpMesh.Vertices[i],
                };
                if (assimpMesh.HasNormals)
                    iovertex.Normal = assimpMesh.Normals[i];
                if (assimpMesh.HasTangentBasis)
                {
                    iovertex.Tangent = assimpMesh.Tangents[i];
                    iovertex.Binormal = assimpMesh.BiTangents[i];
                }

                for (int u = 0; u < assimpMesh.TextureCoordinateChannelCount; u++)
                {
                    if (assimpMesh.HasTextureCoords(u))
                        iovertex.SetUV(
                             assimpMesh.TextureCoordinateChannels[u][i].X,
                             assimpMesh.TextureCoordinateChannels[u][i].Y, u);
                }
                for (int c = 0; c < assimpMesh.VertexColorChannelCount; c++)
                {
                    if (assimpMesh.HasVertexColors(c))
                        iovertex.SetColor(
                             assimpMesh.VertexColorChannels[c][i].X,
                             assimpMesh.VertexColorChannels[c][i].Y,
                             assimpMesh.VertexColorChannels[c][i].Z,
                             assimpMesh.VertexColorChannels[c][i].W, c);
                }

                foreach (var weight in envelopes[i].Weights)
                    iovertex.Envelope.Weights.Add(weight);

                iomesh.Vertices.Add(iovertex);
            }

            IOPolygon iopolygon = new IOPolygon();
            iomesh.Polygons.Add(iopolygon);

            for (int i = 0; i < assimpMesh.FaceCount; i++)
            {
                // Trangle
                iopolygon.Indicies.Add(assimpMesh.Faces[i].Indices[0]);
                iopolygon.Indicies.Add(assimpMesh.Faces[i].Indices[1]);
                iopolygon.Indicies.Add(assimpMesh.Faces[i].Indices[2]);
            }

            if (assimpMesh.MaterialIndex != -1 && assimpMesh.MaterialIndex < scene.MaterialCount)
                iopolygon.MaterialName = scene.Materials[assimpMesh.MaterialIndex].Name;

            iomesh.TransformVertices(worldTansform);

            return iomesh;
        }

        private IOAnimation LoadAnimation(Animation animation)
        {
            float frameRate = (float)animation.TicksPerSecond;

            IOAnimation ioanim = new IOAnimation()
            {
                EndFrame = (float)animation.DurationInTicks,  
                FrameRate = (float)animation.TicksPerSecond,
            };

            float GetFrame(double tick)
            {
                return (float)tick;
            }

            ioanim.Name = animation.Name;
            for (int i = 0; i < animation.NodeAnimationChannelCount; i++)
            {
                var node = animation.NodeAnimationChannels[i];
                IOAnimation nodeAnim = new IOAnimation()
                {
                    Name = node.NodeName, 
                };
                ioanim.Groups.Add(nodeAnim);

                bool IsPreviousValue(IOAnimationTrack track, double value)
                {
                    if (track.KeyFrames.Count > 0 && (float)track.KeyFrames.LastOrDefault().Value == (float)value)
                        return true;
                    return false;
                }

                if (node.HasPositionKeys)
                {
                    IOAnimationTrack positionTrackX = new IOAnimationTrack(IOAnimationTrackType.PositionX);
                    IOAnimationTrack positionTrackY = new IOAnimationTrack(IOAnimationTrackType.PositionY);
                    IOAnimationTrack positionTrackZ = new IOAnimationTrack(IOAnimationTrackType.PositionZ);
                     

                    foreach (var key in node.PositionKeys)
                    {
                        if (!IsPreviousValue(positionTrackX, key.Value.X))
                            positionTrackX.KeyFrames.Add(new IOKeyFrame(GetFrame(key.Time), key.Value.X));
                        if (!IsPreviousValue(positionTrackY, key.Value.Y))
                            positionTrackY.KeyFrames.Add(new IOKeyFrame(GetFrame(key.Time), key.Value.Y));
                        if (!IsPreviousValue(positionTrackZ, key.Value.Z))
                            positionTrackZ.KeyFrames.Add(new IOKeyFrame(GetFrame(key.Time), key.Value.Z));
                    }

                    if (positionTrackX.HasKeys) nodeAnim.Tracks.Add(positionTrackX);
                    if (positionTrackY.HasKeys) nodeAnim.Tracks.Add(positionTrackY);
                    if (positionTrackZ.HasKeys) nodeAnim.Tracks.Add(positionTrackZ);
                }
                if (node.HasRotationKeys)
                {
                    IOAnimationTrack rotationTrackX = new IOAnimationTrack(IOAnimationTrackType.RotationEulerX);
                    IOAnimationTrack rotationTrackY = new IOAnimationTrack(IOAnimationTrackType.RotationEulerY);
                    IOAnimationTrack rotationTrackZ = new IOAnimationTrack(IOAnimationTrackType.RotationEulerZ);

                    foreach (var key in node.RotationKeys)
                    {
                        // Quat to euler key
                        var eulerKey = key.Value.ToEuler();

                        if (!IsPreviousValue(rotationTrackX, eulerKey.X))
                            rotationTrackX.KeyFrames.Add(new IOKeyFrame(GetFrame(key.Time), eulerKey.X));
                        if (!IsPreviousValue(rotationTrackY, eulerKey.Y))
                            rotationTrackY.KeyFrames.Add(new IOKeyFrame(GetFrame(key.Time), eulerKey.Y));
                        if (!IsPreviousValue(rotationTrackZ, eulerKey.Z))
                            rotationTrackZ.KeyFrames.Add(new IOKeyFrame(GetFrame(key.Time), eulerKey.Z));
                    }

                    if (rotationTrackX.HasKeys) nodeAnim.Tracks.Add(rotationTrackX);
                    if (rotationTrackY.HasKeys) nodeAnim.Tracks.Add(rotationTrackY);
                    if (rotationTrackZ.HasKeys) nodeAnim.Tracks.Add(rotationTrackZ);
                }
                if (node.HasScalingKeys)
                {
                    IOAnimationTrack scaleTrackX = new IOAnimationTrack(IOAnimationTrackType.ScaleX);
                    IOAnimationTrack scaleTrackY = new IOAnimationTrack(IOAnimationTrackType.ScaleY);
                    IOAnimationTrack scaleTrackZ = new IOAnimationTrack(IOAnimationTrackType.ScaleZ);

                    foreach (var key in node.ScalingKeys)
                    {
                        if (!IsPreviousValue(scaleTrackX, key.Value.X))
                            scaleTrackX.KeyFrames.Add(new IOKeyFrame(GetFrame(key.Time), key.Value.X));
                        if (!IsPreviousValue(scaleTrackY, key.Value.Y))
                            scaleTrackY.KeyFrames.Add(new IOKeyFrame(GetFrame(key.Time), key.Value.Y));
                        if (!IsPreviousValue(scaleTrackZ, key.Value.Z))
                            scaleTrackZ.KeyFrames.Add(new IOKeyFrame(GetFrame(key.Time), key.Value.Z));
                    }

                    if (scaleTrackX.HasKeys) nodeAnim.Tracks.Add(scaleTrackX);
                    if (scaleTrackY.HasKeys) nodeAnim.Tracks.Add(scaleTrackY);
                    if (scaleTrackZ.HasKeys) nodeAnim.Tracks.Add(scaleTrackZ);
                }
            }
            return ioanim;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string[] GetExtensions()
        {
            return new string[] { ".fbx" };
        }

        public string[] GetSupportedImportFormats()
        {
            using AssimpContext assimpContext = new AssimpContext();
            return assimpContext.GetSupportedImportFormats();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string Name()
        {
            return "Autodesk FBX";
        }

        public bool Verify(string filePath)
        {
            return Path.GetExtension(filePath).ToLower().Equals(".fbx") && AssimpHelper.IsRuntimePresent();
        }
    }
}
