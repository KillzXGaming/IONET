using IONET.Core;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace IONET.SMD
{
    public class SMDExporter : ISceneExporter
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="filePath"></param>
        public void ExportScene(IOScene scene, string filePath, ExportSettings settings)
        {
            if (scene.Models.Count == 0)
                return;

            using (FileStream stream = new FileStream(filePath, FileMode.Create))
            using (StreamWriter w = new StreamWriter(stream))
            {
                var model = scene.Models[0];

                w.WriteLine("version 1");

                var bones = model.Skeleton.BreathFirstOrder();

                Dictionary<string, int> nodeToID = new Dictionary<string, int>();

                w.WriteLine("nodes");
                var index = 0;
                foreach(var b in bones)
                {
                    nodeToID.Add(b.Name, index);
                    w.WriteLine($"{index++} \"{b.Name}\" {model.Skeleton.IndexOf(b.Parent)}");
                }
                foreach (var b in model.Meshes)
                {
                    nodeToID.Add(b.Name, index);
                    w.WriteLine($"{index++} \"{b.Name}\" {-1}");
                }
                w.WriteLine("end");


                w.WriteLine("skeleton");

                if (scene.Animations.Count > 0)
                {
                    for (int i = 0; i <= scene.Animations[0].GetFrameCount(); i++)
                    {
                        w.WriteLine($"time {i}");
                        foreach (var group in scene.Animations[0].Groups)
                        {
                            if (!nodeToID.ContainsKey(group.Name))
                                continue;

                            var idx = nodeToID[group.Name];
                            var b = bones.FirstOrDefault(x => x.Name == group.Name);

                            var pos = new Vector3(b.TranslationX, b.TranslationY, b.TranslationZ);
                            var rot = new Vector3(b.RotationEuler.X, b.RotationEuler.Y, b.RotationEuler.Z);

                            foreach (var track in group.Tracks)
                            {
                                switch (track.ChannelType)
                                {
                                    case Core.Animation.IOAnimationTrackType.PositionX: pos.X = track.GetFrameValue(i); break;
                                    case Core.Animation.IOAnimationTrackType.PositionY: pos.Y = track.GetFrameValue(i); break;
                                    case Core.Animation.IOAnimationTrackType.PositionZ: pos.Z = track.GetFrameValue(i); break;
                                    case Core.Animation.IOAnimationTrackType.RotationEulerX: rot.X = track.GetFrameValue(i); break;
                                    case Core.Animation.IOAnimationTrackType.RotationEulerY: rot.Y = track.GetFrameValue(i); break;
                                    case Core.Animation.IOAnimationTrackType.RotationEulerZ: rot.Z = track.GetFrameValue(i); break;
                                }
                            }
                            w.WriteLine($"{idx} {pos.X} {pos.Y} {pos.Z} {rot.X} {rot.Y} {rot.Z}");
                        }
                    }
                }
                else
                {
                    w.WriteLine("time 0");
                    index = 0;
                    foreach (var b in bones)
                        w.WriteLine($"{index++} {b.TranslationX} {b.TranslationY} {b.TranslationZ} {b.RotationEuler.X} {b.RotationEuler.Y} {b.RotationEuler.Z}");
                }

                foreach (var b in model.Meshes)
                    w.WriteLine($"{index++} 0 0 0 0 0 0");

                w.WriteLine("end");


                if(model.Meshes.Count > 0)
                {
                    w.WriteLine("triangles");

                    foreach(var m in model.Meshes)
                    {
                        // smd only supports triangles
                        m.MakeTriangles();

                        // look though all poly groups
                        for (int p = 0; p < m.Polygons.Count; p ++)
                        {
                            var poly = m.Polygons[p];
                            for (int i = 0; i < poly.Indicies.Count; i += 3)
                            {
                                if(string.IsNullOrEmpty(poly.MaterialName))
                                    w.WriteLine($"{m.Name}_poly_{p}");
                                else
                                    w.WriteLine($"{poly.MaterialName}");

                                {
                                    var v1 = m.Vertices[poly.Indicies[i]];
                                    var env = v1.Envelope;
                                    w.WriteLine($"{nodeToID[m.Name]} {v1.Position.X} {v1.Position.Y} {v1.Position.Z} {v1.Normal.X} {v1.Normal.Y} {v1.Normal.Z} {(v1.UVs.Count > 0 ? v1.UVs[0].X : 0)} {(v1.UVs.Count > 0 ? v1.UVs[0].Y : 0)} {env.Weights.Count + " " + string.Join(" ", env.Weights.Select(e => nodeToID[e.BoneName] + " " + e.Weight))}");
                                }
                                {
                                    var v1 = m.Vertices[poly.Indicies[i + 1]];
                                    var env = v1.Envelope;
                                    w.WriteLine($"{nodeToID[m.Name]} {v1.Position.X} {v1.Position.Y} {v1.Position.Z} {v1.Normal.X} {v1.Normal.Y} {v1.Normal.Z} {(v1.UVs.Count > 0 ? v1.UVs[0].X : 0)} {(v1.UVs.Count > 0 ? v1.UVs[0].Y : 0)} {env.Weights.Count + " " + string.Join(" ", env.Weights.Select(e => nodeToID[e.BoneName] + " " + e.Weight))}");
                                }
                                {
                                    var v1 = m.Vertices[poly.Indicies[i + 2]];
                                    var env = v1.Envelope;
                                    w.WriteLine($"{nodeToID[m.Name]} {v1.Position.X} {v1.Position.Y} {v1.Position.Z} {v1.Normal.X} {v1.Normal.Y} {v1.Normal.Z} {(v1.UVs.Count > 0 ? v1.UVs[0].X : 0)} {(v1.UVs.Count > 0 ? v1.UVs[0].Y : 0)} {env.Weights.Count + " " + string.Join(" ", env.Weights.Select(e => nodeToID[e.BoneName] + " " + e.Weight))}");
                                }
                            }
                        }
                    }

                    w.WriteLine("end");
                }
            }
        }
        

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string[] GetExtensions()
        {
            return new string[] { ".smd" };
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string Name()
        {
            return "StudioMdl";
        }
    }
}
