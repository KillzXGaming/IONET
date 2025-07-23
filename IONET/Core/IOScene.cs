using IONET.Core.Model;
using IONET.Core.Animation;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using IONET.Core.Light;
using IONET.Core.Camera;
using System.Numerics;
using IONET.Collada.B_Rep.Topology;
using System;

namespace IONET.Core
{
    public class IOScene
    {
        /// <summary>
        /// 
        /// </summary>
        public string Name { get; set; } = "Scene";

        /// <summary>
        /// 
        /// </summary>
        public List<IONode> Nodes { get; internal set; } = new List<IONode>();

        /// <summary>
        /// 
        /// </summary>
        public List<IOModel> Models { get; internal set; } = new List<IOModel>();

        /// <summary>
        /// 
        /// </summary>
        public List<IOLight> Lights { get; internal set; } = new List<IOLight>();

        /// <summary>
        /// 
        /// </summary>
        public List<IOCamera> Cameras { get; internal set; } = new List<IOCamera>();

        /// <summary>
        /// 
        /// </summary>
        public List<IOMaterial> Materials { get; internal set; } = new List<IOMaterial>();

        /// <summary>
        /// 
        /// </summary>
        public List<IOAnimation> Animations { get; internal set; } = new List<IOAnimation>();

        /// <summary>
        /// Cleans material names to allow for smoother export
        /// </summary>
        public void CleanMaterialNames()
        {
            Dictionary<string, string> nameToName = new Dictionary<string, string>();

            foreach (var mat in Materials)
            {
                var sanatize = Regex.Replace(mat.Name.Trim(), @"\s+", "_").Replace("#", "");
                nameToName.Add(mat.Name, sanatize);
                mat.Name = sanatize;
            }

            foreach (var mod in Models)
                foreach (var mesh in mod.Meshes)
                    foreach (var poly in mesh.Polygons)
                        if (nameToName.ContainsKey(poly.MaterialName))
                            poly.MaterialName = nameToName[poly.MaterialName];
        }

        public void LoadSkeletonFromNodes(IOModel model)
        {
            foreach (var node in this.Nodes)
                if (node.Parent == null)
                    LoadSkeletonFromNodes(node, null, Matrix4x4.Identity, this, model, model.GetSkinnedBoneList());
        }

        public void LoadSkeletonFromNodes(IOModel model, List<string> skeletonIds)
        {
            foreach (var node in this.Nodes)
                if (node.Parent == null)
                    LoadSkeletonFromNodes(node, null, Matrix4x4.Identity, this, model, skeletonIds);
        }

        public void LoadSkeletonFromNodes(IONode bone, IONode parent, Matrix4x4 parentMatrix, IOScene scene, IOModel model, List<string> skeletonIds)
        {
            bool isBone = !string.IsNullOrEmpty(bone.Name) && skeletonIds.Contains(bone.Name) || (bone.IsJoint);

            var p = isBone ? bone : null;

            // detect skeleton
            //Check if bone is rigged in the skeleton id list or if node is a joint type and no root has been set yet
            if (bone.Name != "Armature")
            {
                if ((!string.IsNullOrEmpty(bone.Name) && skeletonIds.Contains(bone.Name)) ||
                (bone.IsJoint && parent == null) || bone.Name == "Root")
                {
                    //Root found. Apply parent node transform and add to skeleton
                    model.Skeleton.RootBones.Add(bone);
                    bone.LocalTransform *= parentMatrix;

                    return;
                }
            }

            // load children
            foreach (IONode v in bone.Children)
                LoadSkeletonFromNodes(v, p, bone.LocalTransform * parentMatrix, scene, model, skeletonIds);
        }

        public void PrintNodeHierachy()
        {
            foreach (var n in this.Nodes)
                if (n.Parent == null)
                    PrintNode(n);
        }

        public void PrintNode(IONode parent = null, string ind = "")
        {
            if (parent.Mesh != null)
                Console.WriteLine($"{ind}Mesh {parent.Name}");
            else if (parent.Light != null)
                Console.WriteLine($"{ind}Light {parent.Name}");
            else if (parent.Camera != null)
                Console.WriteLine($"{ind}Camera {parent.Name}");
            else
                Console.WriteLine($"{ind}Node {parent.Name}");

            string inc = ind + "-";
            foreach (IONode n in parent.Children)
                PrintNode(n, inc);
        }
    }
}
