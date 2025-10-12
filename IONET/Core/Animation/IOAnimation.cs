using IONET.Core.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace IONET.Core.Animation
{
    public class IOAnimation
    {
        /// <summary>
        /// 
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public List<IOAnimation> Groups { get; internal set; } = new List<IOAnimation>();

        /// <summary>
        /// 
        /// </summary>
        public List<IOAnimationTrack> Tracks { get; internal set; } = new List<IOAnimationTrack>();

        /// <summary>
        /// 
        /// </summary>
        public float StartFrame { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public float EndFrame { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public bool UseSegmentScaleCompensate { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public float GetFrameCount()
        {
            if (EndFrame != 0)
                return EndFrame - StartFrame;

            float frameCount = 0;
            foreach (var group in Groups)
                frameCount = Math.Max(frameCount, group.GetFrameCount());
            foreach (var track in Tracks)
            {
                //Important to +1 as the frame is the currently played frame in a keyframe
                var frame = track.KeyFrames.Max(x => x.Frame) + 1;
                //Get largest key frame value
                frameCount = Math.Max(frameCount, frame);
            }
            return frameCount;
        }

        public void ApplySegmentScaleCompensate(List<IOModel> models)
        {
            var model = models.FirstOrDefault();
            var skeleton = model.Skeleton;

            foreach (var group in this.Groups)
            {
                if (group.UseSegmentScaleCompensate)
                {
                    var bone = skeleton.GetBoneByName(group.Name);
                    if (bone == null)
                        continue;

                    var parent = bone.Parent;
                    if (parent == null)
                        continue;

                    var parentAnim = this.Groups.FirstOrDefault(x => x.Name == parent.Name);
                    if (parentAnim == null)
                        continue;

                    foreach (var track in group.Tracks)
                    {
                        foreach (var parentTrack in parentAnim.Tracks)
                        {
                            if (track.ChannelType != parentTrack.ChannelType)
                                continue;

                            if (track.ChannelType == IOAnimationTrackType.ScaleX)
                                ApplySegmentScaleCompensate(track, parentTrack);
                            if (track.ChannelType == IOAnimationTrackType.ScaleY)
                                ApplySegmentScaleCompensate(track, parentTrack);
                            if (track.ChannelType == IOAnimationTrackType.ScaleZ)
                                ApplySegmentScaleCompensate(track, parentTrack);
                        }
                    }
                }
            }
        }

        private void ApplySegmentScaleCompensate(IOAnimationTrack track, IOAnimationTrack parentTrack)
        {
            //Gather all key values usable
            List<int> allKeys = new List<int>();
            foreach (var key in track.KeyFrames)
            {
                int k = (int)Math.Floor(key.Frame + 0.01);
                if (allKeys.FindIndex((v) => { return k == v; }) < 0)
                {
                    allKeys.Add(k);
                }
            }
            foreach (var key in parentTrack.KeyFrames)
            {
                int k = (int)Math.Floor(key.Frame + 0.01);
                if (allKeys.FindIndex((v) => { return k == v; }) < 0)
                {
                    allKeys.Add(k);
                }
            }
            allKeys.Sort();
            foreach (int k in allKeys)
            {
                float v = track.GetFrameValue(k) / parentTrack.GetFrameValue(k);
                track.InsertKeyframe(k, v);

                Console.WriteLine($"ApplySegmentScaleCompensate {track.ChannelType} {v}");
            }
        }

        public override string ToString()
        {
            return $"{Name}";
        }
    }
}
