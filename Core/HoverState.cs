using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Runs;

namespace NodeForge
{
    /// <summary>Tracks the map node currently under the mouse (updated from NMapPoint.OnFocus/OnUnfocus).</summary>
    internal static class HoverState
    {
        public static MapPoint? Current { get; private set; }
        public static IRunState? RunState { get; private set; }
        public static MapPointState CurrentState { get; private set; } = MapPointState.None;

        public static void Focus(MapPoint? point, IRunState? runState, MapPointState state)
        {
            Current = point;
            RunState = runState;
            CurrentState = state;
            Menu2Overlay.Instance?.OnHoverChanged();
        }

        public static void Unfocus(MapPoint? point)
        {
            if (ReferenceEquals(Current, point))
            {
                Current = null;
                CurrentState = MapPointState.None;
                Menu2Overlay.Instance?.OnHoverChanged();
            }
        }
    }
}
