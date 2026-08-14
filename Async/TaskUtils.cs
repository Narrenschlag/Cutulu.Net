namespace Cutulu.Async;

using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System;
using Core;

public static class TaskUtils
{
#if GODOT4_0_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async Task NextFrame()
    {
        var tree = (Godot.SceneTree)Godot.Engine.GetMainLoop();
        await tree.ToSignal(tree, Godot.SceneTree.SignalName.ProcessFrame);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async Task SyncToProcess() => await NextFrame();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async Task NextPhysicsFrame()
    {
        var tree = (Godot.SceneTree)Godot.Engine.GetMainLoop();
        await tree.ToSignal(tree, Godot.SceneTree.SignalName.PhysicsFrame);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsOnMainThread() => Application.IsOnMainThread();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async Task SyncToPhysicsProcess() => await NextPhysicsFrame();

    public static async Task WaitFrames(int frames)
    {
        var tree = (Godot.SceneTree)Godot.Engine.GetMainLoop();
        for (int i = 0; i < frames; i++)
            await tree.ToSignal(tree, Godot.SceneTree.SignalName.ProcessFrame);
    }

    public static async Task WaitAnimation(this Godot.AnimationPlayer animationPlayer, string animationName)
    {
        if (animationPlayer.IsNull() || animationName.IsEmpty() || animationPlayer.HasAnimation(animationName) == false) return;

        animationPlayer.Play(animationName);

        if (animationPlayer.IsPlaying())
            await WaitSeconds(animationPlayer.GetCurrentAnimationLength(), true);
    }
#endif

    public static async Task WaitUntil(Func<bool> condition, bool waitForMainThread = true)
    {
        while (condition() == false)
            await Task.Delay(1);

#if GODOT4_0_OR_GREATER
        if (waitForMainThread) await SyncToProcess();
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async Task WaitSeconds(double seconds, bool waitForMainThread = true)
    => await WaitSeconds((float)seconds, waitForMainThread);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async Task WaitSeconds(float seconds, bool waitForMainThread = true)
    => await WaitMilliseconds((int)(seconds * 1000), waitForMainThread);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async Task WaitMilliseconds(int milliseconds, bool waitForMainThread = true)
    {
        await Task.Delay(milliseconds);

#if GODOT4_0_OR_GREATER
        if (waitForMainThread) await SyncToProcess();
#endif
    }
}