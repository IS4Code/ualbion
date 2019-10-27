﻿using System.Collections.Generic;
using UAlbion.Core;
using UAlbion.Game.Events;

namespace UAlbion.Game.State
{
    public class SceneStack : Component
    {
        static readonly HandlerSet Handlers = new HandlerSet(
            H<SceneStack, PushSceneEvent>((x, e) =>
            {
                x._stack.Push(x._sceneId);
                x._sceneId = e.SceneId;
                x.Raise(new SetSceneEvent(e.SceneId));
            }),
            H<SceneStack, PopSceneEvent>((x, e) =>
            {
                if (x._stack.Count > 0)
                {
                    var newSceneId = x._stack.Pop();
                    x._sceneId = newSceneId;
                    x.Raise(new SetSceneEvent(newSceneId));
                }
            }),
            H<SceneStack, SetSceneEvent>((x, e) =>
            {
                x._stack.Clear();
                x._sceneId = e.SceneId;
            })
        );

        readonly Stack<SceneId> _stack = new Stack<SceneId>();
        SceneId _sceneId;
        public SceneStack() : base(Handlers) { }
    }
}