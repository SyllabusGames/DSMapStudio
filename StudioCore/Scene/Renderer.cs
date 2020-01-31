﻿using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Veldrid;

namespace StudioCore.Scene
{
    public class Renderer
    {
        public class RenderQueue
        {
            private struct KeyIndex : IComparable<KeyIndex>, IComparable
            {
                public RenderKey Key { get; }
                public int ItemIndex { get; }

                public KeyIndex(RenderKey key, int itemIndex)
                {
                    Key = key;
                    ItemIndex = itemIndex;
                }

                public int CompareTo(object obj)
                {
                    return ((IComparable)Key).CompareTo(obj);
                }

                public int CompareTo(KeyIndex other)
                {
                    return Key.CompareTo(other.Key);
                }

                public override string ToString()
                {
                    return string.Format("Index:{0}, Key:{1}", ItemIndex, Key);
                }
            }

            public SceneRenderPipeline Pipeline { get; private set; }
            private GraphicsDevice Device;
            private CommandList DrawCommandList;

            private readonly List<KeyIndex> Indices = new List<KeyIndex>(1000);
            private readonly List<RenderObject> Renderables = new List<RenderObject>(1000);

            private Action<GraphicsDevice, CommandList> PreDrawSetup = null;

            private Pipeline ActivePipeline = null;

            public int Count => Renderables.Count;

            public float CPURenderTime { get; private set; } = 0.0f;

            public RenderQueue(GraphicsDevice device, SceneRenderPipeline pipeline)
            {
                Device = device;
                Pipeline = pipeline;
                DrawCommandList = device.ResourceFactory.CreateCommandList();
            }

            public void SetPredrawSetupAction(Action<GraphicsDevice, CommandList> setup)
            {
                PreDrawSetup = setup;
            }

            public void Clear()
            {
                Indices.Clear();
                Renderables.Clear();
            }

            public void Add(RenderObject item, RenderKey key)
            {
                int index = Renderables.Count;
                Indices.Add(new KeyIndex(key, index));
                Renderables.Add(item);
            }

            private void Sort()
            {
                Indices.Sort();
            }

            public void Execute()
            {
                var watch = Stopwatch.StartNew();
                Sort();
                ActivePipeline = null;
                DrawCommandList.Begin();
                PreDrawSetup.Invoke(Device, DrawCommandList);
                foreach (var obj in Indices)
                {
                    var o = Renderables[obj.ItemIndex];
                    o.UpdatePerFrameResources(Device, DrawCommandList, Pipeline);
                }
                foreach (var obj in Indices)
                {
                    var o = Renderables[obj.ItemIndex];
                    var p = o.GetPipeline();
                    if (p != ActivePipeline)
                    {
                        DrawCommandList.SetPipeline(p);
                        Renderer.VertexBufferAllocator.BindAsVertexBuffer(DrawCommandList);
                        ActivePipeline = p;
                    }
                    o.Render(Device, DrawCommandList, Pipeline);
                }
                DrawCommandList.End();
                Device.SubmitCommands(DrawCommandList);
                watch.Stop();
                CPURenderTime = (float)(((double)watch.ElapsedTicks / (double)Stopwatch.Frequency) * 1000.0);
            }
        }

        private static GraphicsDevice Device;
        private static CommandList MainCommandList;

        private static Queue<Action<GraphicsDevice, CommandList>> RenderWorkQueue;
        private static List<RenderQueue> RenderQueues;
        private static Queue<Action<GraphicsDevice, CommandList>> BackgroundUploadQueue;

        public static GPUBufferAllocator VertexBufferAllocator { get; private set; }
        public static GPUBufferAllocator IndexBufferAllocator { get; private set; }

        public static ResourceFactory Factory
        {
            get
            {
                return Device.ResourceFactory;
            }
        }

        public static void Initialize(GraphicsDevice device)
        {
            Device = device;
            MainCommandList = device.ResourceFactory.CreateCommandList();
            RenderWorkQueue = new Queue<Action<GraphicsDevice, CommandList>>();
            BackgroundUploadQueue = new Queue<Action<GraphicsDevice, CommandList>>();
            RenderQueues = new List<RenderQueue>();

            VertexBufferAllocator = new GPUBufferAllocator(2 * 1024 * 1024 * 1024u, BufferUsage.VertexBuffer);
            IndexBufferAllocator = new GPUBufferAllocator(1024 * 1024 * 1024, BufferUsage.IndexBuffer);
        }

        public static void RegisterRenderQueue(RenderQueue queue)
        {
            RenderQueues.Add(queue);
        }

        public static void AddBackgroundUploadTask(Action<GraphicsDevice, CommandList> action)
        {
            lock (BackgroundUploadQueue)
            {
                BackgroundUploadQueue.Enqueue(action);
            }
        }

        public static void Frame()
        {
            MainCommandList.Begin();

            Queue<Action<GraphicsDevice, CommandList>> work;
            lock (BackgroundUploadQueue)
            {
                work = new Queue<Action<GraphicsDevice, CommandList>>(BackgroundUploadQueue);
                BackgroundUploadQueue.Clear();
            }
            while (work.Count() > 0)
            {
                work.Dequeue().Invoke(Device, MainCommandList);
            }

            MainCommandList.End();
            Device.SubmitCommands(MainCommandList);

            foreach (var rq in RenderQueues)
            {
                rq.Execute();
                rq.Clear();
            }
        }
    }
}
