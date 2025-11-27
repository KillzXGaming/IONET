using IONET.Core.Model;
using IONET.Core;
using IONET.Fbx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Assimp;
using System.Numerics;
using System.Linq;
using IONET.Assimp;
using IONET.Core.Skeleton;
using IONET.Core.Animation;

namespace IONET.AssimpLib
{
    public class AssimpExport : ISceneExporter
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="ioscene"></param>
        /// <param name="filePath"></param>
        /// <param name="settings"></param>
        public void ExportScene(IOScene ioscene, string filePath, ExportSettings settings)
        {
            PostProcessSteps process = PostProcessSteps.None;

            Scene scene = new Scene();
            scene.RootNode = new Node("RootNode");

            SaveMaterialList(scene, ioscene);

            foreach (var model in ioscene.Models)
            {
                SaveBoneList(scene, ioscene, model);
                SaveMeshList(scene, ioscene, model);
            }

            using var context = new AssimpContext();

            string ext = Path.GetExtension(filePath).ToLower();

            string formatID = "collada";

            if (ext == ".obj") formatID = "obj";
            if (ext == ".3ds") formatID = "3ds";
            if (ext == ".dae") formatID = "collada";
            if (ext == ".ply") formatID = "ply";
            if (ext == ".fbx") formatID = "fbx";
            if (ext == ".fbx") formatID = "fbx";
            if (ext == ".glb") formatID = "glb";
            if (ext == ".gltf") formatID = "gltf";
            if (ext == ".x") formatID = "x";


            bool success = context.ExportFile(scene, filePath, formatID, process);
        }

        private void SaveBoneList(Scene scene, IOScene ioscene, IOModel model)
        {
            Node skeleton = new Node("skeleton_root");

            if (model.Skeleton.RootBones.Count == 1)
            {
                foreach (IOBone node in model.Skeleton.RootBones)
                    scene.RootNode.Children.Add(SaveBone(node));
            }
            else
            {
                foreach (IOBone node in model.Skeleton.RootBones)
                    skeleton.Children.Add(SaveBone(node));

                if (skeleton.HasChildren)
                    scene.RootNode.Children.Add(skeleton);
            }
        }

        private Node SaveBone(IOBone ionode)
        {
            Node assimpNode = new Node(ionode.Name)
            {
                Transform = ionode.LocalTransform.FromNumerics(),
            };
            if (ionode is IONode n && n.Mesh != null)
                n.Mesh.ParentBone = n;

            foreach (IOBone child in ionode.Children)
                assimpNode.Children.Add(SaveBone(child));

            return assimpNode;
        }

        private void SaveMaterialList(Scene scene, IOScene ioscene)
        {
            TextureWrapMode ConvertWrap(WrapMode mode)
            {
                switch (mode)
                {
                    case WrapMode.MIRROR: return TextureWrapMode.Mirror;
                    case WrapMode.REPEAT: return TextureWrapMode.Wrap;
                    case WrapMode.CLAMP: return TextureWrapMode.Clamp;
                }
                return TextureWrapMode.Wrap;
            }

            TextureSlot ConvertSlot(IOTexture iotexture, TextureType type)
            {
                if (iotexture == null || string.IsNullOrEmpty(iotexture.FilePath))
                    return new TextureSlot();

                return new TextureSlot()
                {
                    FilePath = iotexture.FilePath,
                    WrapModeU = ConvertWrap(iotexture.WrapS),
                    WrapModeV = ConvertWrap(iotexture.WrapT),
                    UVIndex = iotexture.UVChannel,
                    TextureType = type,  
                };
            }

            foreach (var iomaterial in ioscene.Materials)
            {
                Material assimpMaterial = new Material()
                {
                    Name = iomaterial.Name,
                };
                scene.Materials.Add(assimpMaterial);

                if (iomaterial.DiffuseMap != null) assimpMaterial.TextureDiffuse = ConvertSlot(iomaterial.DiffuseMap, TextureType.Diffuse);
                if (iomaterial.NormalMap != null) assimpMaterial.TextureNormal = ConvertSlot(iomaterial.NormalMap, TextureType.Normals);
                if (iomaterial.SpecularMap != null) assimpMaterial.TextureSpecular = ConvertSlot(iomaterial.SpecularMap, TextureType.Specular);
                if (iomaterial.EmissionMap != null) assimpMaterial.TextureEmissive = ConvertSlot(iomaterial.EmissionMap, TextureType.Emissive);
                if (iomaterial.AmbientMap != null) assimpMaterial.TextureAmbient = ConvertSlot(iomaterial.AmbientMap, TextureType.Ambient);
                if (iomaterial.ReflectiveMap != null) assimpMaterial.TextureReflection = ConvertSlot(iomaterial.ReflectiveMap, TextureType.Reflection);
                if (iomaterial.AmbientOcclusionMap != null) assimpMaterial.TextureAmbientOcclusion = ConvertSlot(iomaterial.AmbientOcclusionMap, TextureType.AmbientOcclusion);

                assimpMaterial.ColorDiffuse = iomaterial.DiffuseColor;
                assimpMaterial.ColorSpecular = iomaterial.SpecularColor;
                assimpMaterial.ColorEmissive = iomaterial.EmissionColor;
                assimpMaterial.Shininess = iomaterial.Shininess;
            }

            // Seems required, even with -1 material index, so set a default material.
            if (scene.Materials.Count == 0)
            {
                Material assimpMaterial = new Material()
                {
                    Name = "DefaultMaterial",
                };
                scene.Materials.Add(assimpMaterial);
            }
        }

        private void SaveMeshList(Scene scene, IOScene ioscene, IOModel iomodel)
        {
            var boneList = iomodel.Skeleton.BreathFirstOrder();
            List<string> missingBones = new List<string>();

            foreach (var iomesh in iomodel.Meshes)
            {
                Mesh assimpMesh = new Mesh()
                {
                    Name = iomesh.Name,
                    MaterialIndex = 0,
                    PrimitiveType = PrimitiveType.Triangle,
                };
                scene.Meshes.Add(assimpMesh);


                var n = new Node()
                {
                    Name = assimpMesh.Name,
                    Transform = Matrix4x4.Identity,
                };

                bool hasNode = false;

                // Find matching node attachment
                if (iomesh.ParentBone != null)
                {
                    var node = scene.RootNode.FindNode(iomesh.ParentBone.Name);
                    if (node != null)
                    {
                        n = node;
                        hasNode = true;
                    }
                }

                n.MeshIndices.Add(scene.Meshes.Count - 1);

                if (!hasNode)
                    scene.RootNode.Children.Add(n);

                for (int v = 0; v < iomesh.Vertices.Count; v++)
                {
                    var vertex = iomesh.Vertices[v];

                    assimpMesh.Vertices.Add(vertex.Position);
                    assimpMesh.Normals.Add(vertex.Normal);
                    assimpMesh.Tangents.Add(vertex.Tangent);
                    assimpMesh.BiTangents.Add(vertex.Binormal);

                    for (int u = 0; u < vertex.UVs.Count; u++)
                    {
                        assimpMesh.TextureCoordinateChannels[u].Add(
                            new Vector3(vertex.UVs[u].X, vertex.UVs[u].Y, u));

                        if (assimpMesh.UVComponentCount[u] == 0)
                            assimpMesh.UVComponentCount[u] = 2;
                    }

                    for (int c = 0; c < vertex.Colors.Count; c++)
                        assimpMesh.VertexColorChannels[c].Add(vertex.Colors[c]);

                    foreach (var boneWeight in vertex.Envelope.Weights)
                    {
                        var iobone = boneList.FirstOrDefault(x => x.Name == boneWeight.BoneName);
                        if (iobone == null)
                        {
                            if (!missingBones.Contains(boneWeight.BoneName))
                                missingBones.Add(boneWeight.BoneName);
                            continue;
                        }

                        int boneInd = assimpMesh.Bones.FindIndex(x => x.Name == boneWeight.BoneName);
                        if (boneInd == -1) // Assign a bone that is used for rigging
                        {
                            boneInd = assimpMesh.Bones.Count;
                            assimpMesh.Bones.Add(new Bone() { Name = boneWeight.BoneName });

                            // Assign matrices
                            Matrix4x4.Invert(iobone.WorldTransform, out Matrix4x4 inverted);
                            assimpMesh.Bones[boneInd].OffsetMatrix = inverted;
                        }

                        // Assign weights
                        assimpMesh.Bones[boneInd].VertexWeights.Add(new VertexWeight()
                        {
                            Weight = boneWeight.Weight,
                            VertexID = v,
                        });
                    }
                }

                foreach (var poly in iomesh.Polygons)
                {
                    var idx = ioscene.Materials.FindIndex(x => x.Name == poly.MaterialName);
                        if (idx != -1)
                            assimpMesh.MaterialIndex = idx;
                        else if (!string.IsNullOrEmpty(poly.MaterialName))
                            Console.WriteLine($"Failed to find material {poly.MaterialName}");

                    for (int i = 0; i < poly.Indicies.Count; i += 3)
                    {
                        // No valid triangle, skip
                        if (i >= poly.Indicies.Count - 2)
                            break;

                        assimpMesh.Faces.Add(new Face(new int[3]
                        {
                                poly.Indicies[i + 0],
                                poly.Indicies[i + 1],
                                poly.Indicies[i + 2],
                        }));
                    }
                }
            }
            foreach (var bone in missingBones)
                Console.WriteLine($"Missing {bone} in skeleton!");
        }

        private Animation SaveAnimation(IOAnimation ioanim)
        {
            Animation anim = new Animation()
            {
                Name = ioanim.Name,
                DurationInTicks = ioanim.GetFrameCount(),
                TicksPerSecond = ioanim.FrameRate,
            };

             foreach (var group in ioanim.Groups)
            {
                NodeAnimationChannel node = new NodeAnimationChannel()
                {
                    NodeName = ioanim.Name,
                };
                anim.NodeAnimationChannels.Add(node);

                foreach (var track in group.Tracks)
                {
                    switch (track.ChannelType)
                    {
                        case IOAnimationTrackType.PositionX:
                            break;
                        case IOAnimationTrackType.PositionY:
                            break;
                        case IOAnimationTrackType.PositionZ:
                            break;
                        case IOAnimationTrackType.ScaleX:
                            break;
                        case IOAnimationTrackType.ScaleY:
                            break;
                        case IOAnimationTrackType.ScaleZ:
                            break;
                    }
                }
            }
            return anim;
        }

        public ExportFormatDescription[] GetSupportedExportFormats()
        {
            using AssimpContext assimpContext = new AssimpContext();
            return assimpContext.GetSupportedExportFormats();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string[] GetExtensions()
        {
            return new string[] { ".fbx" };
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string Name()
        {
            return "Autodesk FBX";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public bool Verify(string filePath)
        {
            foreach (var exte in GetSupportedExportFormats())
            {
                if (Path.GetExtension(filePath).ToLower().Equals($".{exte.FileExtension}"))
                    return true;
            }

            return Path.GetExtension(filePath).ToLower().Equals(".fbx") && AssimpHelper.IsRuntimePresent();
        }
    }
}
