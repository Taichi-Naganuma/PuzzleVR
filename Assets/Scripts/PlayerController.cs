using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Week 2 学習演習: Input System + MonoBehaviour ライフサイクル の動作観察用コンポーネント。
///
/// 目的:
///   1. Awake / Start / OnEnable / OnDisable / OnDestroy の発火順を Console で観察する
///   2. InputActionAsset の購読 (Enable) / 解放 (Disable) の作法を実機で確認する
///   3. SerializeField で Inspector に値を露出する書き方を反復する
///
/// 注意:
///   - Phase 2 プロト本体ではない（Phase 1 完成後 + Quest 3 到着後に着手）
///   - Quest / XR Controller への依存は持たせない（XR binding は Quest 到着後に追加）
///   - Editor 上で WASD / Gamepad 左スティックでの XZ 平面移動が確認できれば DoD 達成
/// </summary>
public class PlayerController : MonoBehaviour
{
    // --- Inspector exposed fields -------------------------------------------------

    [Tooltip("Player Map に Move / Jump / Look を持つ InputActionAsset を割り当てる。")]
    [SerializeField] private InputActionAsset _inputActions;

    [Tooltip("transform.Translate に掛ける速度（m/s）。")]
    [SerializeField] private float _moveSpeed = 5f;

    [Tooltip("ライフサイクルログを Console に出力するか。学習演習用。")]
    [SerializeField] private bool _verboseLifecycleLog = true;

    // --- Cached action references -------------------------------------------------

    private InputActionMap _playerMap;
    private InputAction _moveAction;

    // --- MonoBehaviour lifecycle --------------------------------------------------

    private void Awake()
    {
        LogLifecycle(nameof(Awake));

        if (_inputActions == null)
        {
            Debug.LogError(
                $"[{nameof(PlayerController)}] InputActionAsset が割り当てられていません。" +
                "Inspector で PuzzleVRInputActions.inputactions をセットしてください。",
                this);
            return;
        }

        _playerMap = _inputActions.FindActionMap("Player", throwIfNotFound: false);
        if (_playerMap == null)
        {
            Debug.LogError(
                $"[{nameof(PlayerController)}] InputActionAsset に 'Player' Map が見つかりません。",
                this);
            return;
        }

        _moveAction = _playerMap.FindAction("Move", throwIfNotFound: false);
        if (_moveAction == null)
        {
            Debug.LogError(
                $"[{nameof(PlayerController)}] 'Player/Move' アクションが見つかりません。",
                this);
        }
    }

    private void OnEnable()
    {
        LogLifecycle(nameof(OnEnable));

        // Input System 作法: 有効化はここで行う。
        // Awake で参照解決済みのアクションを毎回 Enable する。
        _playerMap?.Enable();
    }

    private void Start()
    {
        LogLifecycle(nameof(Start));
    }

    private void Update()
    {
        if (_moveAction == null)
        {
            return;
        }

        Vector2 input = _moveAction.ReadValue<Vector2>();

        // Vector2 を XZ 平面に投影（Y は 0、上下移動はしない）。
        // ローカル空間で Translate するため、オブジェクトの向きに応じて前後左右が変わる。
        Vector3 delta = new Vector3(input.x, 0f, input.y) * (_moveSpeed * Time.deltaTime);
        transform.Translate(delta, Space.Self);
    }

    private void OnDisable()
    {
        LogLifecycle(nameof(OnDisable));

        // Input System 作法: OnDisable で必ず Disable を呼んでクリーンアップする。
        // これを怠るとシーン遷移時にコールバックが残留してリークやヌル参照の温床になる。
        _playerMap?.Disable();
    }

    private void OnDestroy()
    {
        LogLifecycle(nameof(OnDestroy));
    }

    // --- helpers ------------------------------------------------------------------

    private void LogLifecycle(string methodName)
    {
        if (!_verboseLifecycleLog)
        {
            return;
        }

        Debug.Log($"[{nameof(PlayerController)}][{Time.frameCount}] {methodName}", this);
    }
}
