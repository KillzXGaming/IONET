using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IONET.Core.Animation
{
    public class IOKeyFrame : ICloneable
    {
        /// <summary>
        /// 
        /// </summary>
        public float Time { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public float Frame { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public object Value { get; set; }

        public float ValueF32
        {
            get { return (float)Value; }
            set { Value = value; }
        }

        public IOKeyFrame() { }

        public IOKeyFrame(float frame, float value)
        {
            this.Frame = frame;
            this.Value = value;
        }

        public virtual object Clone()
        {
            return new IOKeyFrame()
            {
                Frame = this.Frame,
                Time = this.Time,
                Value = this.Value,
            };
        }

        public override string ToString()
        {
            return $"{Frame} {Value}";
        }
    }

    public class IOKeyFrameStep : IOKeyFrame
    {

    }

    public class IOKeyFrameHermite : IOKeyFrame
    {
        /// <summary>
        /// 
        /// </summary>
        public float TangentSlopeInput { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public float TangentSlopeOutput { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public float TangentWeightInput { get; set; } = 1.0f;
        /// <summary>
        /// 
        /// </summary>
        public float TangentWeightOutput { get; set; } = 1.0f;

        /// <summary>
        /// 
        /// </summary>
        public bool IsWeighted => (TangentWeightInput != 1.0f || TangentWeightOutput != 1.0f);

        public static IOKeyFrameHermite FromBezier(IOKeyFrameBezier bezier)
        {
            return new IOKeyFrameHermite()
            {
                Frame = bezier.Frame,
                Value = bezier.Value,
            };
        }

        public override object Clone()
        {
            return new IOKeyFrameHermite()
            {
                Frame = this.Frame,
                Time = this.Time,
                Value = this.Value,
                TangentSlopeInput = this.TangentSlopeInput,
                TangentSlopeOutput = this.TangentSlopeOutput,
                TangentWeightInput = this.TangentWeightInput,
                TangentWeightOutput = this.TangentWeightOutput,
            };
        }

        public override string ToString()
        {
            return $"{Frame} {Value} {TangentSlopeInput} {TangentSlopeOutput}";
        }
    }

    public class IOKeyFrameBezier : IOKeyFrame
    {
        /// <summary>
        /// 
        /// </summary>
        public float TangentInputX { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public float TangentInputY { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public float TangentOutputX { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public float TangentOutputY { get; set; }

        public override object Clone()
        {
            return new IOKeyFrameBezier()
            {
                Frame = this.Frame,
                Time = this.Time,
                Value = this.Value,
                TangentInputX = this.TangentInputX,
                TangentInputY = this.TangentInputY,
                TangentOutputX = this.TangentOutputX,
                TangentOutputY = this.TangentOutputY,
            };
        }
    }
}
