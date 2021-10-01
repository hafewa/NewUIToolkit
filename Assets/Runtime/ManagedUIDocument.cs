using System;
using Naninovel;
using Naninovel.UI;
using TMPro;
using UniRx.Async;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Allows using Unity's UI Toolkit documents as Naninovel UIs.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class ManagedUIDocument : MonoBehaviour, IManagedUI
{
    [Serializable]
    public class GameState
    {
        public bool Visible;
    }

    public event Action<bool> OnVisibilityChanged;

    public virtual bool Visible { get => visible; set => SetVisible(value); }
    public virtual bool Interactable { get; set; }
    public virtual Camera RenderCamera { get => renderCamera; set => SetRenderCamera(value); }

    protected virtual UIDocument Document { get; private set; }
    protected virtual VisualElement Root => Document.rootVisualElement;
    protected virtual float FadeTime => fadeTime;
    protected virtual bool SaveVisibilityState => saveVisibilityState;
    protected virtual bool VisibleOnAwake => visibleOnAwake;

    [Tooltip("Default opacity fade duration (in seconds) when changing visibility.")]
    [SerializeField] private float fadeTime = 0.33f;
    [Tooltip("Whether to preserve visibility of the UI when saving/loading game.")]
    [SerializeField] private bool saveVisibilityState = true;
    [Tooltip("Whether the UI should be initially visible.")]
    [SerializeField] private bool visibleOnAwake = true;

    private readonly Tweener<FloatTween> opacityTweener = new();
    private IStateManager stateManager;
    private Camera renderCamera;
    private bool visible;

    public virtual UniTask InitializeAsync () => UniTask.CompletedTask;

    public virtual async UniTask ChangeVisibilityAsync (bool visible, float? duration = null,
        AsyncToken asyncToken = default)
    {
        HandleVisibilityChanged(visible);
        var from = Root.style.opacity.value;
        var to = visible ? 1f : 0f;
        var tween = new FloatTween(from, to, duration ?? FadeTime, v => Root.style.opacity = v);
        if (visible) Root.visible = true;
        await opacityTweener.RunAsync(tween, asyncToken, Document);
        if (!visible) Root.visible = false;
    }

    public virtual void SetFont (Font font, TMP_FontAsset tmpFont) { }

    public virtual void SetFontSize (int size) { }

    protected virtual void SetVisible (bool visible)
    {
        if (opacityTweener.Running) opacityTweener.CompleteInstantly();
        Root.style.opacity = visible ? 1 : 0;
        Root.visible = visible;
        HandleVisibilityChanged(visible);
    }

    protected virtual void HandleVisibilityChanged (bool visible)
    {
        this.visible = visible;
        Root.SetEnabled(visible);
        OnVisibilityChanged?.Invoke(visible);
    }

    protected virtual void SetRenderCamera (Camera camera)
    {
        renderCamera = camera;
        Document.panelSettings.targetTexture = camera.targetTexture;
    }

    protected virtual void SerializeState (GameStateMap stateMap)
    {
        if (SaveVisibilityState)
        {
            var state = new GameState {
                Visible = Visible
            };
            stateMap.SetState(state, name);
        }
    }

    protected virtual UniTask DeserializeState (GameStateMap stateMap)
    {
        if (SaveVisibilityState)
        {
            var state = stateMap.GetState<GameState>(name);
            if (state is null) return UniTask.CompletedTask;
            Visible = state.Visible;
        }
        return UniTask.CompletedTask;
    }

    private void Awake ()
    {
        Document = GetComponent<UIDocument>();
        stateManager = Engine.GetService<IStateManager>();
        Visible = VisibleOnAwake;
    }

    private void OnEnable ()
    {
        stateManager.AddOnGameSerializeTask(SerializeState);
        stateManager.AddOnGameDeserializeTask(DeserializeState);
    }

    private void OnDisable ()
    {
        if (stateManager != null)
        {
            stateManager.RemoveOnGameSerializeTask(SerializeState);
            stateManager.RemoveOnGameDeserializeTask(DeserializeState);
        }
    }
}
