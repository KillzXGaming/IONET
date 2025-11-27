using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IONET.Core.Skeleton;

namespace IONET.Core.Animation
{
    public class IOAnimationTrack
    {
        /// <summary>
        /// 
        /// </summary>
        public IOAnimationTrackType ChannelType { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public IOCurveWrapMode PreWrap { get; set; } = IOCurveWrapMode.Constant;

        /// <summary>
        /// 
        /// </summary>
        public IOCurveWrapMode PostWrap { get; set; } = IOCurveWrapMode.Constant;

        /// <summary>
        /// 
        /// </summary>
        public List<IOKeyFrame> KeyFrames { get; internal set; } = new List<IOKeyFrame>();

        public bool HasKeys => KeyFrames.Count > 0;

        public IOAnimationTrack() { }

        public IOAnimationTrack(IOAnimationTrackType channelType)
        {
            ChannelType = channelType;
        }

        public override string ToString()
        {
            return $"{ChannelType} Keys: " + string.Join("|", this.KeyFrames);
        }

        public void InsertKeyframe(float frame, float value, float inSlope, float outSlope)
        {
            var key = KeyFrames.FirstOrDefault(x => x.Frame == frame);
            if (key != null)
            {
                ((IOKeyFrameHermite)key).Value = value;
                ((IOKeyFrameHermite)key).TangentSlopeInput = inSlope;
                ((IOKeyFrameHermite)key).TangentSlopeOutput = outSlope;
            }
            else
            {
                KeyFrames.Add(new IOKeyFrameHermite()
                {
                    Frame = frame,
                    Value = value,
                    TangentSlopeInput = inSlope,
                    TangentSlopeOutput = outSlope
                });
            }
        }

        public void InsertKeyframe(float frame, float[] value, float[] inSlope, float[] outSlope)
        {
            var key = KeyFrames.FirstOrDefault(x => x.Frame == frame);
            if (key != null)
            {
                ((IOKeyFrameHermite)key).Value = value;
                ((IOKeyFrameHermite)key).TangentSlopeInput = inSlope[0]; //todo
                ((IOKeyFrameHermite)key).TangentSlopeOutput = outSlope[0]; //todo
            }
            else
            {
                KeyFrames.Add(new IOKeyFrameHermite()
                {
                    Frame = frame,
                    Value = value,
                    TangentSlopeInput = inSlope[0],
                    TangentSlopeOutput = outSlope[0]
                });
            }
        }

        public void InsertKeyframe(float frame, float value)
        {
            var key = KeyFrames.FirstOrDefault(x=> x.Frame == frame);
            if (key != null)
                key.Value = value;
            else
            {
                KeyFrames.Add(new IOKeyFrame()
                {
                    Frame = frame,
                    Value = value,
                }); 
            }
        }

        public void InsertKeyframe(float frame, float[] value)
        {
            var key = KeyFrames.FirstOrDefault(x => x.Frame == frame);
            if (key != null)
                key.Value = value;
            else
            {
                KeyFrames.Add(new IOKeyFrame()
                {
                    Frame = frame,
                    Value = value,
                });
            }
        }

        public float GetFrameValue(float frame)
        {
            if (KeyFrames.Count == 0) return 0;
            if (KeyFrames.Count == 1) return KeyFrames[0].ValueF32;
            IOKeyFrame LK = KeyFrames.First();
            IOKeyFrame RK = KeyFrames.Last();

            float Frame = GetWrapFrame(frame, PostWrap);
            foreach (IOKeyFrame keyFrame in KeyFrames)
            {
                if (keyFrame.Frame <= Frame) LK = keyFrame;
                if (keyFrame.Frame >= Frame && keyFrame.Frame < RK.Frame) RK = keyFrame;
            }

            if (LK.Frame != RK.Frame)
            {
                float FrameDiff = Frame - LK.Frame;
                float Weight = FrameDiff / (RK.Frame - LK.Frame);
                if (LK is IOKeyFrameHermite)
                {
                    float length = RK.Frame - LK.Frame;

                    if (!(RK is IOKeyFrameHermite))
                        RK = new IOKeyFrameHermite() { Value = RK.Value };

                    IOKeyFrameHermite hermiteKeyLK = (IOKeyFrameHermite)LK;
                    IOKeyFrameHermite hermiteKeyRK = (IOKeyFrameHermite)RK;

                    return InterpolationHelper.HermiteInterpolate(Frame,
                      hermiteKeyLK.Frame,
                      hermiteKeyRK.Frame,
                      hermiteKeyLK.ValueF32,
                      hermiteKeyRK.ValueF32,
                      hermiteKeyLK.TangentSlopeOutput * length,
                      hermiteKeyRK.TangentSlopeInput * length);
                }
                else
                    return InterpolationHelper.Lerp(LK.ValueF32, RK.ValueF32, Weight);
            }
            return LK.ValueF32;
        }

        private float GetWrapFrame(float frame, IOCurveWrapMode mode)
        {
            var lastFrame = KeyFrames.Last().Frame;
            if (mode == IOCurveWrapMode.Constant)
            {
                if (frame > lastFrame)
                    return lastFrame;
                else
                    return frame;
            }
            if (mode == IOCurveWrapMode.Cycle)
            {
                while (frame > lastFrame)
                    frame -= lastFrame;
                return frame;
            }
            return frame;
        }
    }
}
