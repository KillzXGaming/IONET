using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.IO;
using IONET.Core.Skeleton;
using IONET.Core.Animation;
using IONET.Core.IOMath;
using System.Globalization;
using System.Threading;

namespace IONET.MayaAnim
{
    public class MayaAnim
    {
        private enum InfinityType
        {
            constant,
            linear,
            cycle,
            cycleRelative,
            oscillate
        }

        private enum InputType
        {
            time,
            unitless
        }

        private enum OutputType
        {
            time,
            linear,
            angular,
            unitless
        }

        private enum ControlType
        {
            translate,
            rotate,
            scale,
            visibility
        }

        private enum TrackType
        {
            translateX,
            translateY,
            translateZ,
            rotateX,
            rotateY,
            rotateZ,
            scaleX,
            scaleY,
            scaleZ,
            visibility
        }

        private class Header
        {
            public float animVersion;
            public string mayaVersion;
            public float startTime;
            public float endTime;
            public float startUnitless;
            public float endUnitless;
            public string timeUnit;
            public string linearUnit;
            public string angularUnit;

            public Header()
            {
                animVersion = 1.1f;
                mayaVersion = "2015";
                startTime = 1;
                endTime = 1;
                startUnitless = 0;
                endUnitless = 0;
                timeUnit = "ntscf";
                linearUnit = "cm";
                angularUnit = "rad";
            }
        }

        private class AnimKey
        {
            public float input, output;
            public string intan, outtan;
            public float t1 = 0, w1 = 1;
            public float t2 = 0, w2 = 1;

            public AnimKey()
            {
                intan = "linear";
                outtan = "linear";
            }
        }

        private class AnimData
        {
            public ControlType controlType;
            public TrackType type;
            public InputType input;
            public OutputType output;
            public InfinityType preInfinity, postInfinity;
            public bool weighted = false;
            public List<AnimKey> keys = new List<AnimKey>();

            public AnimData()
            {
                input = InputType.time;
                output = OutputType.linear;
                preInfinity = InfinityType.constant;
                postInfinity = InfinityType.constant;
                weighted = false;
            }
        }

        private class AnimBone
        {
            public string name;
            public List<AnimData> atts = new List<AnimData>();
        }

        private Header header;
        private List<AnimBone> Bones = new List<AnimBone>();

        public string Name = "Animation";

        public MayaAnim()
        {
            header = new Header();
        }

        public void Open(string fileName)
        {
            var originalCulture = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture; // or new CultureInfo("en-US")
                Parse(fileName);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = originalCulture; // restore
            }
        }

        private void Parse(string fileName)
        {
            Name = Path.GetFileNameWithoutExtension(fileName);
            using (StreamReader r = new StreamReader(new FileStream(fileName, FileMode.Open)))
            {
                AnimData currentData = null;
                while (!r.EndOfStream)
                {
                    var line = r.ReadLine();
                    var args = line.Trim().Replace(";", "").Split(' ');

                    switch (args[0])
                    {
                        case "animVersion":
                            header.animVersion = float.Parse(args[1]);
                            break;
                        case "mayaVersion":
                            header.mayaVersion = args[1];
                            break;
                        case "timeUnit":
                            header.timeUnit = args[1];
                            break;
                        case "linearUnit":
                            header.linearUnit = args[1];
                            break;
                        case "angularUnit":
                            header.angularUnit = args[1];
                            break;
                        case "startTime":
                            header.startTime = float.Parse(args[1]);
                            break;
                        case "endTime":
                            header.endTime = float.Parse(args[1]);
                            break;
                        case "anim":
                            if (args.Length != 7)
                                continue;
                            var currentNode = Bones.Find(e => e.name.Equals(args[3]));
                            if (currentNode == null)
                            {
                                currentNode = new AnimBone();
                                currentNode.name = args[3];
                                Bones.Add(currentNode);
                            }
                            currentData = new AnimData();
                            currentData.controlType = (ControlType)Enum.Parse(typeof(ControlType), args[1].Split('.')[0]);
                            currentData.type = (TrackType)Enum.Parse(typeof(TrackType), args[2]);
                            currentNode.atts.Add(currentData);
                            break;
                        case "animData":
                            if (currentData == null)
                                continue;
                            string dataLine = r.ReadLine();
                            while (!dataLine.Contains("}"))
                            {
                                var dataArgs = dataLine.Trim().Replace(";", "").Split(' ');
                                switch (dataArgs[0])
                                {
                                    case "input":
                                        currentData.input = (InputType)Enum.Parse(typeof(InputType), dataArgs[1]);
                                        break;
                                    case "output":
                                        currentData.output = (OutputType)Enum.Parse(typeof(OutputType), dataArgs[1]);
                                        break;
                                    case "weighted":
                                        currentData.weighted = dataArgs[1] == "1";
                                        break;
                                    case "preInfinity":
                                        currentData.preInfinity = (InfinityType)Enum.Parse(typeof(InfinityType), dataArgs[1]);
                                        break;
                                    case "postInfinity":
                                        currentData.postInfinity = (InfinityType)Enum.Parse(typeof(InfinityType), dataArgs[1]);
                                        break;
                                    case "keys":
                                        string keyLine = r.ReadLine();
                                        while (!keyLine.Contains("}"))
                                        {
                                            var keyArgs = keyLine.Trim().Replace(";", "").Split(' ');

                                            var key = new AnimKey()
                                            {
                                                input = float.Parse(keyArgs[0]),
                                                output = float.Parse(keyArgs[1])
                                            };

                                            if (keyArgs.Length >= 7)
                                            {
                                                key.intan = keyArgs[2];
                                                key.outtan = keyArgs[3];
                                            }

                                            if (key.intan == "fixed" || key.intan == "spline")
                                            {
                                                if (keyArgs.Length > 7)
                                                    key.t1 = float.Parse(keyArgs[7]);
                                                if (keyArgs.Length > 8)
                                                    key.w1 = float.Parse(keyArgs[8]);
                                            }
                                            if ((key.outtan == "fixed" || key.intan == "spline") && keyArgs.Length > 9)
                                            {
                                                if (keyArgs.Length > 9)
                                                    key.t2 = float.Parse(keyArgs[9]);
                                                if (keyArgs.Length > 10)
                                                    key.w2 = float.Parse(keyArgs[10]);
                                            }

                                            currentData.keys.Add(key);

                                            keyLine = r.ReadLine();
                                        }
                                        break;

                                }
                                dataLine = r.ReadLine();
                            }
                            break;
                    }
                }
            }
        }

        public void Save(string fileName)
        {
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(fileName))
            {
                file.WriteLine("animVersion " + header.animVersion + ";");
                file.WriteLine("mayaVersion " + header.mayaVersion + ";");
                file.WriteLine("timeUnit " + header.timeUnit + ";");
                file.WriteLine("linearUnit " + header.linearUnit + ";");
                file.WriteLine("angularUnit " + header.angularUnit + ";");
                file.WriteLine("startTime " + 1 + ";");
                file.WriteLine("endTime " + header.endTime + ";");

                int Row = 0;

                foreach (AnimBone animBone in Bones)
                {
                    int TrackIndex = 0;
                    if (animBone.atts.Count == 0)
                    {
                        file.WriteLine($"anim {animBone.name} 0 1 {TrackIndex++};");
                    }
                    foreach (AnimData animData in animBone.atts)
                    {
                        file.WriteLine($"anim {animData.controlType}.{animData.type} {animData.type} {animBone.name} 0 1 {TrackIndex++};");
                        file.WriteLine("animData {");
                        file.WriteLine($" input {animData.input};");
                        file.WriteLine($" output {animData.output};");
                        file.WriteLine($" weighted {(animData.weighted ? 1 : 0)};");
                        file.WriteLine($" preInfinity {animData.preInfinity};");
                        file.WriteLine($" postInfinity {animData.postInfinity};");

                        file.WriteLine(" keys {");
                        foreach (AnimKey key in animData.keys)
                        {
                            string tanin = key.intan == "spline" || key.intan == "fixed" || key.intan == "auto" ? " " + key.t1 + " " + key.w1 : "";
                            string tanout = key.intan == "spline" || key.outtan == "fixed" || key.outtan == "auto" ? " " + key.t2 + " " + key.w2 : "";
                            file.WriteLine($" {key.input} {key.output:N6} {key.intan} {key.outtan} 1 1 0{tanin}{tanout};");
                        }
                        file.WriteLine(" }");

                        file.WriteLine("}");
                    }
                    Row++;
                }
            }
        }

        public static IOAnimation ImportAnimation(string filePath, ImportSettings settings)
        {
            var anim = new MayaAnim();
            anim.Open(filePath);
            return anim.GetAnimation();
        }

        public IOAnimation GetAnimation()
        {
            IOAnimation anim = new IOAnimation();
            anim.Name = this.Name;
            anim.StartFrame = this.header.startTime;
            anim.EndFrame = this.header.endTime;
            foreach (var mayaAnimBone in this.Bones)
            {
                IOAnimation animBone = new IOAnimation();
                animBone.Name = mayaAnimBone.name;
                anim.Groups.Add(animBone);

                foreach (var att in mayaAnimBone.atts)
                    animBone.Tracks.Add(GetTrack(att));
            }
            return anim;
        }

        private IOAnimationTrack GetTrack(AnimData data)
        {
            IOAnimationTrack track = new IOAnimationTrack();
            switch (data.type)
            {
                case TrackType.translateX: track.ChannelType = IOAnimationTrackType.PositionX; break;
                case TrackType.translateY: track.ChannelType = IOAnimationTrackType.PositionY; break;
                case TrackType.translateZ: track.ChannelType = IOAnimationTrackType.PositionZ; break;
                case TrackType.rotateX: track.ChannelType = IOAnimationTrackType.RotationEulerX; break;
                case TrackType.rotateY: track.ChannelType = IOAnimationTrackType.RotationEulerY; break;
                case TrackType.rotateZ: track.ChannelType = IOAnimationTrackType.RotationEulerZ; break;
                case TrackType.scaleX: track.ChannelType = IOAnimationTrackType.ScaleX; break;
                case TrackType.scaleY: track.ChannelType = IOAnimationTrackType.ScaleY; break;
                case TrackType.scaleZ: track.ChannelType = IOAnimationTrackType.ScaleZ; break;
                case TrackType.visibility: track.ChannelType = IOAnimationTrackType.NodeVisibility; break;
            }
            track.PreWrap = CurveWrapModes.FirstOrDefault(x => x.Value == data.preInfinity).Key;
            track.PostWrap = CurveWrapModes.FirstOrDefault(x => x.Value == data.postInfinity).Key;

            bool isRotate = track.ChannelType == IOAnimationTrackType.RotationEulerX ||
                            track.ChannelType == IOAnimationTrackType.RotationEulerY ||
                            track.ChannelType == IOAnimationTrackType.RotationEulerZ;

            foreach (var key in data.keys)
            {
                if (key.intan == "fixed" || key.outtan == "fixed" ||
                    key.intan == "spline" || key.outtan == "spline")
                {
                    track.KeyFrames.Add(new IOKeyFrameHermite()
                    {
                        Frame = key.input - header.startTime,
                        Value = GetOutputValue(this, data, key.output, isRotate),
                        TangentSlopeInput = GetOutputValue(this, data, key.t1, isRotate),
                        TangentSlopeOutput = GetOutputValue(this, data, key.t2, isRotate),
                        TangentWeightInput = key.w1,
                        TangentWeightOutput = key.w2,
                    });
                }
                else
                {
                    track.KeyFrames.Add(new IOKeyFrame()
                    {
                        Frame = key.input - header.startTime,
                        Value = GetOutputValue(this, data, key.output, isRotate),
                    });
                }
            }

            return track;
        }

        static Dictionary<IOCurveWrapMode, InfinityType> CurveWrapModes = new Dictionary<IOCurveWrapMode, InfinityType>()
        {
            { IOCurveWrapMode.Constant, InfinityType.constant },
            { IOCurveWrapMode.Linear, InfinityType.linear },
            { IOCurveWrapMode.Cycle, InfinityType.cycle },
            { IOCurveWrapMode.CycleRelative, InfinityType.cycleRelative },
            { IOCurveWrapMode.Oscillate, InfinityType.oscillate },
        };

        private float GetOutputValue(MayaAnim anim, AnimData data, float value, bool isRotate)
        {
            if (data.output == OutputType.angular || isRotate)
            {
                if (anim.header.angularUnit == "deg")
                    return (float)(value * (Math.PI / 180));
            }
            return value;
        }

        public static void ExportAnimation(string filePath, ExportSettings settings, IOAnimation animation, IOSkeleton skeleton)
        {
            var anim = CreateMayaAnimation(settings, animation, skeleton);
            anim.Save(filePath);
        }

        static MayaAnim CreateMayaAnimation(ExportSettings settings, IOAnimation animation, IOSkeleton skeleton)
        {
            MayaAnim anim = new MayaAnim();
            anim.header.startTime = 0;
            anim.header.endTime = animation.EndFrame != 0 ? animation.EndFrame :  animation.GetFrameCount();
            if (!settings.MayaAnimUseRadians)
                anim.header.angularUnit = "deg";

            // get bone order
            List<IOBone> BonesInOrder = getBoneTreeOrder(skeleton);
            if (settings.MayaAnim2015)
                BonesInOrder = BonesInOrder.OrderBy(f => f.Name, StringComparer.Ordinal).ToList();

            foreach (IOBone b in BonesInOrder)
            {
                AnimBone animBone = new AnimBone()
                {
                    name = b.Name
                };
                anim.Bones.Add(animBone);

                Console.WriteLine($"AnimBone {animBone.name}");

                var group = animation.Groups.FirstOrDefault(x => x.Name.Equals(b.Name));
                if (group == null)
                {
                    Console.WriteLine($"Bone not found {group}! Skipping");
                    continue;
                }

                foreach (var track in group.Tracks)
                {
                    List<IOKeyFrame> keyFrames = track.KeyFrames;
                    switch (track.ChannelType)
                    {
                        case IOAnimationTrackType.PositionX:
                            AddAnimData(settings, animBone, track, keyFrames, ControlType.translate, TrackType.translateX);
                            break;
                        case IOAnimationTrackType.PositionY:
                            AddAnimData(settings, animBone, track, keyFrames, ControlType.translate, TrackType.translateY);
                            break;
                        case IOAnimationTrackType.PositionZ:
                            AddAnimData(settings, animBone, track, keyFrames, ControlType.translate, TrackType.translateZ);
                            break;
                        case IOAnimationTrackType.RotationEulerX:
                            AddAnimData(settings, animBone, track, keyFrames, ControlType.rotate, TrackType.rotateX);
                            break;
                        case IOAnimationTrackType.RotationEulerY:
                            AddAnimData(settings, animBone, track, keyFrames, ControlType.rotate, TrackType.rotateY);
                            break;
                        case IOAnimationTrackType.RotationEulerZ:
                            AddAnimData(settings, animBone, track, keyFrames, ControlType.rotate, TrackType.rotateZ);
                            break;
                        case IOAnimationTrackType.ScaleX:
                            AddAnimData(settings, animBone, track, keyFrames, ControlType.scale, TrackType.scaleX);
                            break;
                        case IOAnimationTrackType.ScaleY:
                            AddAnimData(settings, animBone, track, keyFrames, ControlType.scale, TrackType.scaleY);
                            break;
                        case IOAnimationTrackType.ScaleZ:
                            AddAnimData(settings, animBone, track, keyFrames, ControlType.scale, TrackType.scaleZ);
                            break;
                    }
                }
            }
            return anim;
        }

        static void AddAnimData(ExportSettings settings, AnimBone animBone, IOAnimationTrack track, List<IOKeyFrame> keyFrames, ControlType ctype, TrackType ttype)
        {
            AnimData d = new AnimData();
            d.controlType = ctype;
            d.type = ttype;
            d.preInfinity = CurveWrapModes[track.PreWrap];
            d.postInfinity = CurveWrapModes[track.PostWrap];
            //Check if any tangents include weights.
            d.weighted = keyFrames.Any(x => x is IOKeyFrameHermite && ((IOKeyFrameHermite)x).IsWeighted);

            bool isAngle = ctype == ControlType.rotate;
            if (isAngle)
                d.output = OutputType.angular;

            float value = keyFrames.Count > 0 ? keyFrames[0].ValueF32 : 0;

            bool IsConstant = true;
            foreach (var key in keyFrames)
            {
                if ((float)key.ValueF32 != value) {
                    IsConstant = false;
                    break;
                }
            }
            foreach (var key in keyFrames)
            {
                AnimKey animKey = new AnimKey()
                {
                    input = key.Frame + 1,
                    output = isAngle ? GetAngle(settings, key.ValueF32) : key.ValueF32,
                };
                if (key is IOKeyFrameHermite)
                {
                    animKey.intan = "fixed";
                    animKey.outtan = "fixed";
                    animKey.t1 = ((IOKeyFrameHermite)key).TangentSlopeInput;
                    animKey.t2 = ((IOKeyFrameHermite)key).TangentSlopeOutput;
                    animKey.w1 = ((IOKeyFrameHermite)key).TangentWeightInput;
                    animKey.w2 = ((IOKeyFrameHermite)key).TangentWeightOutput;

                    animKey.t1 = MathF.Atan(animKey.t1);
                    animKey.t2 = MathF.Atan(animKey.t2);

                    if (isAngle && !settings.MayaAnimUseRadians)
                    {
                        animKey.t1 = MathExt.RadToDeg(animKey.t1);
                        animKey.t2 = MathExt.RadToDeg(animKey.t2);
                    }
                }
                d.keys.Add(animKey);
                if (IsConstant)
                    break;
            }

            if (d.keys.Count > 0)
                animBone.atts.Add(d);
        }

        private static float GetAngle(ExportSettings settings, float value) {
            return (settings.MayaAnimUseRadians ? value : (float)(value * (180 / Math.PI)));
        }

        private static List<IOBone> getBoneTreeOrder(IOSkeleton Skeleton)
        {
            if (Skeleton.RootBones.Count == 0)
                return null;
            List<IOBone> bone = new List<IOBone>();
            Queue<IOBone> q = new Queue<IOBone>();

            foreach (IOBone b in Skeleton.RootBones)
            {
                QueueBones(b, q, Skeleton);
            }

            while (q.Count > 0)
            {
                bone.Add(q.Dequeue());
            }
            return bone;
        }

        public static void QueueBones(IOBone b, Queue<IOBone> q, IOSkeleton Skeleton)
        {
            q.Enqueue(b);
            foreach (IOBone c in b.Children)
                QueueBones(c, q, Skeleton);
        }
    }
}
