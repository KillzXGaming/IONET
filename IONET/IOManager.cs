using IONET.Collada;
using IONET.Core;
using IONET.Fbx;
using IONET.SMD;
using IONET.Wavefront;
using IONET.MayaAnim;
using IONET.Core.Model;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Threading;
using System.Globalization;
using IONET.GLTF;
using IONET.AssimpLib;
using IONET.Assimp;

namespace IONET
{
    public class IOManager
    {
        /// <summary>
        /// 
        /// </summary>
        private static ISceneLoader[] SceneLoaders = new
            ISceneLoader[]
        {            
            new AssimpImport(),
            new ColladaImporter(),
            new SMDImporter(),
            new OBJImporter(),
            new FbxImporter(),
            new MayaAnimImporter(),
            new GLTFImporter(),
        };

        /// <summary>
        /// 
        /// </summary>
        private static ISceneExporter[] SceneExporters = new
            ISceneExporter[]
        {
            new AssimpExport(),
            new ColladaExporter(),
            new SMDExporter(),
            new OBJExporter(),
            new MayaAnimExporter(),
            new GLTFExporter(),
        };

        /// <summary>
        /// Gets a file filter for the export formats
        /// </summary>
        /// <returns></returns>
        public static string GetModelExportFileFilter()
        {
            StringBuilder sb = new StringBuilder();

            var allExt = string.Join(";*", SceneExporters.SelectMany(e => e.GetExtensions()));

            sb.Append($"Supported Files (*{allExt})|*{allExt}");

            foreach(var l in SceneExporters)
            {
                var ext = string.Join(";*", l.GetExtensions());
                sb.Append($"|{l.Name()} (*{ext})|*{ext}");
            }

            sb.Append("|All files (*.*)|*.*");

            return sb.ToString();
        }

        /// <summary>
        /// Gets a file filter for the import formats
        /// </summary>
        /// <returns></returns>
        public static string GetModelImportFileFilter()
        {
            StringBuilder sb = new StringBuilder();

            var allExt = string.Join(";*", SceneLoaders.SelectMany(e => e.GetExtensions()));

            sb.Append($"Supported Files (*{allExt})|*{allExt}");

            foreach (var l in SceneLoaders)
            {
                var ext = string.Join(";*", l.GetExtensions());
                sb.Append($"|{l.Name()} (*{ext})|*{ext}");
            }

            sb.Append("|All files (*.*)|*.*");

            return sb.ToString();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public static IOScene LoadScene(string filePath, ImportSettings settings)
        {
            string folder = Path.GetDirectoryName(filePath);
            foreach (var l in SceneLoaders)
                if (l.Verify(filePath))
                {
                    // Disable optimizing for Assimp as that is done automatically in library
                    // Assimp is much better and faster doing this
                    if (l is AssimpImport)
                    {
                        settings.Optimize = false;
                        // Also skip generating tangents/binormals as assimp does this
                        settings.GenerateTangentsAndBinormals = false;
                    }

                    var scene = l.GetScene(filePath, settings);
                    System.Console.WriteLine("Loaded scene!");

                    // apply post processing
                    foreach (var material in scene.Materials)
                    {
                        void SetupPath(Core.Model.IOTexture texture)
                        {
                            if (texture == null || File.Exists(texture.FilePath))
                                return;

                            if (File.Exists($"{folder}//{texture.FilePath}"))
                                texture.FilePath = $"{folder}//{texture.FilePath}";
                        };

                        //Apply absoulte paths
                        SetupPath(material.DiffuseMap);
                        SetupPath(material.AmbientMap);
                        SetupPath(material.EmissionMap);
                        SetupPath(material.ReflectiveMap);
                    }
                    foreach (var model in scene.Models)
                    {
                        // smooth normals
                        if (settings.SmoothNormals)
                            model.SmoothNormals();

                        // post process mesh
                        foreach (var m in model.Meshes)
                        {
                            // optimize vertices
                            if (settings.Optimize)
                                m.Optimize();

                            // make triangles
                            if (settings.Triangulate)
                                m.MakeTriangles();

                            // vertex modifications
                            if (settings.WeightLimit || settings.FlipUVs)
                                foreach (var v in m.Vertices)
                                {
                                    // weight limit
                                    if (settings.WeightLimit)
                                        v.Envelope.Optimize(settings.WeightLimitAmt);
                                    if (settings.WeightNormalize)
                                        v.Envelope.Normalize();

                                    // flip uvs
                                    if (settings.FlipUVs)
                                        for (int i = 0; i < v.UVs.Count; i++)
                                            v.UVs[i] = new System.Numerics.Vector2(v.UVs[i].X, 1 - v.UVs[i].Y);
                                }

                            // flip winding order
                            if (settings.FlipWindingOrder)
                            {
                                foreach (var p in m.Polygons)
                                {
                                    p.ToTriangles(m);

                                    if (p.PrimitiveType == Core.Model.IOPrimitive.TRIANGLE)
                                    {
                                        for (int i = 0; i < p.Indicies.Count; i += 3)
                                        {
                                            var temp = p.Indicies[i + 1];
                                            p.Indicies[i + 1] = p.Indicies[i + 2];
                                            p.Indicies[i + 2] = temp;
                                        }
                                    }
                                }
                            }

                            // generate tangents and bitangants/binormals
                            if (settings.GenerateTangentsAndBinormals)
                                m.GenerateTangentsAndBitangents();

                            // reset envelopes
                           // foreach (var v in m.Vertices)
                            //    v.ResetEnvelope(model.Skeleton);
                        }
                        //Split materials
                        if (settings.SplitMeshMaterials)
                        {
                            List<IOMesh> meshes = new List<IOMesh>();
                            List<int> removeIndices = new List<int>();

                            for (int i = 0; i < model.Meshes.Count; i++)
                            {
                                if (model.Meshes[i].Polygons.Count == 1)
                                    continue;

                                var splitMeshes = model.Meshes[i].SplitByMaterial();
                                meshes.AddRange(splitMeshes);
                            }
                            model.Meshes.AddRange(meshes);
                            meshes.Clear();
                        }
                    }

                    return scene;
                }

            return null;
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="filePath"></param>
        public static void ExportScene(IOScene scene, string filePath, ExportSettings settings = null)
        {
            var current = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");

            var ext = Path.GetExtension(filePath).ToLower();

            if (settings == null)
                settings = new ExportSettings();

            foreach (var l in SceneExporters)
                foreach (var e in l.GetExtensions())
                    if (e.Equals(ext))
                    {
                        // apply post processing
                        foreach (var model in scene.Models)
                        {
                            // smooth normals
                            if (settings.SmoothNormals)
                                model.SmoothNormals();

                            // post process mesh
                            foreach (var m in model.Meshes)
                            {
                                // optimize vertices
                                if (settings.Optimize)
                                    m.Optimize();

                                // vertex modifications
                                if (settings.FlipUVs)
                                    foreach (var v in m.Vertices)
                                    {
                                        // flip uvs
                                        if (settings.FlipUVs)
                                            for (int i = 0; i < v.UVs.Count; i++)
                                                v.UVs[i] = new System.Numerics.Vector2(v.UVs[i].X, 1 - v.UVs[i].Y);
                                    }

                                // flip winding order
                                if (settings.FlipWindingOrder)
                                {
                                    foreach (var p in m.Polygons)
                                    {
                                        p.ToTriangles(m);

                                        if (p.PrimitiveType == Core.Model.IOPrimitive.TRIANGLE)
                                        {
                                            for (int i = 0; i < p.Indicies.Count; i += 3)
                                            {
                                                var temp = p.Indicies[i + 1];
                                                p.Indicies[i + 1] = p.Indicies[i + 2];
                                                p.Indicies[i + 2] = temp;
                                            }
                                        }
                                    }
                                }

                                // reset envelopes
                                foreach (var v in m.Vertices)
                                    v.ResetEnvelope(model.Skeleton);
                            }
                        }

                        foreach (var anim in scene.Animations)
                        {
                            anim.ApplySegmentScaleCompensate(scene.Models);
                        }

                        l.ExportScene(scene, filePath, settings);

                        Thread.CurrentThread.CurrentCulture = current;
                        return;
                    }

            Thread.CurrentThread.CurrentCulture = current;
        }

    }
}
