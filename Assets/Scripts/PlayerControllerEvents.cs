using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Week 2 学習演習(第2回): Player Input コンポーネントの Invoke Unity Events モード版。
///
/// 目的:
///   1. Player Input コンポーネント(UnityEngine.InputSystem.PlayerInput)の Invoke Unity Events
///      モードで Inspector から UnityEvent をバインドする作法を実機で観察する
///   2. 既存 <see cref="PlayerController"/>(C# 直接購読版)と同一シーンに並列共存させ、
///      コード量・リファクタ耐性・学習曲線・性能の比較を体感する
///   3. UnityEvent から呼び出されるメソッドが <see cref="InputAction.CallbackContext"/> を
///      引数として受け取る作法を反復する
///
/// 注意:
///   - Phase 2 プロト本体ではない(Phase 1 完成後 + Quest 3 到着後に着手)
///   - Quest / XR Controller への依存は持たせない(XR binding は Quest 到着後に追加)
///   - 既存 <see cref="PlayerController"/> および RotateCube は削除・改変禁止。並列共存させる
///   - InputActionAsset は <c>Assets/_Puzzle/Input/PuzzleVRInputActions.inputactions</c> を再利用
///
/// Editor 上での attach 手順は
///   `開発指示/20260511_VR_Week2残り_Hiro_結果.md` §3 を参照(John 代行)。
/// </summary>
/// <remarks>
/// <para>
/// 【C# 直接購読版との差分】
/// 直接購読版(<see cref="PlayerController"/>)は <c>Awake</c> で <c>FindActionMap</c>/
/// <c>FindAction</c> を実行し、<c>OnEnable</c>/<c>OnDisable</c> で <c>Enable()</c>/
/// <c>Disable()</c> を明示的に呼ぶ。
/// </para>
/// <para>
/// 一方 Player Input コンポーネント版(本クラス)は、<c>PlayerInput</c> コンポーネント
/// 自身が以下を肩代わりする:
///   - InputActionAsset の保持と <c>Enable()</c>/<c>Disable()</c> 管理
///   - 指定された ActionMap の自動有効化(Default Map / Default Scheme 設定)
///   - 各 Action の発火時に Inspector でバインドされた UnityEvent を呼び出す
/// 本クラス側は「UnityEvent から呼ばれる public メソッド」を提供するだけで済む。
/// </para>
/// <para>
/// 【メソッドシグネチャ規約】
/// Player Input コンポーネント Invoke Unity Events モードで Inspector から呼び出される
/// メソッドは、<see cref="InputAction.CallbackContext"/> 1引数を取る public メソッドである
/// 必要がある(public でないと Inspector のメソッド一覧に出ない)。
/// </para>
/// </remarks>
public class PlayerControllerEvents : MonoBehaviour
{
    // --- Inspector exposed fields -------------------------------------------------

    [Tooltip("transform.Translate に掛ける速度(m/s)。直接購読版と挙動を揃えるため既定 5。")]
    [SerializeField] private float _moveSpeed = 5f;

    [Tooltip("Jump 時の上方向初速(m/s)。Rigidbody 不使用のため、視覚的に分かる定数オフセット用途。")]
    [SerializeField] private float _jumpImpulse = 2f;

    [Tooltip("Look 入力時にログを出力する量(0 で抑制)。Look は値が連続で来るためノイズ削減用の閾値。")]
    [SerializeField] private float _lookLogThreshold = 0.5f;

    [Tooltip("UnityEvent からの呼び出しログを Console に出力するか。学習演習用。")]
    [SerializeField] private bool _verboseEventLog = true;

    // --- Internal state -----------------------------------------------------------

    /// <summary>
    /// 直近フレームの Move 入力(Vector2)。
    /// UnityEvent コールバックは入力デバイスのイベント駆動で発火するため、
    /// 毎フレームの移動適用は <see cref="Update"/> 側で行う(直接購読版との比較しやすさ重視)。
    /// </summary>
    private Vector2 _moveInput;

    // --- MonoBehaviour lifecycle --------------------------------------------------

    private void Update()
    {
        // Move は UnityEvent で受け取った最新値をフレーム毎に消化する。
        // ライフサイクルログは PlayerController.cs 側に集約済みのため、
        // 本クラスでは省略(同一シーンに両方アタッチした際のログ二重出力を避ける)。
        if (_moveInput.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        // Vector2 を XZ 平面に投影(Y は 0、上下移動はしない)。
        // 直接購読版 PlayerController と完全に同じ座標変換規則を採用し、
        // 並列アタッチしたときに挙動が同じであることを目視確認できるようにする。
        Vector3 delta = new Vector3(_moveInput.x, 0f, _moveInput.y) * (_moveSpeed * Time.deltaTime);
        transform.Translate(delta, Space.Self);
    }

    // --- Public callbacks for Player Input (Invoke Unity Events) ------------------

    /// <summary>
    /// Player Input コンポーネントの Move アクション UnityEvent から呼ばれる。
    /// Inspector の Player Input → Events → Player → Move (PlayerControllerEvents.OnMove) で配線する。
    /// </summary>
    /// <param name="context">
    /// Input System が渡すコンテキスト。<c>performed</c>/<c>canceled</c> の両方で呼ばれるため、
    /// <see cref="InputAction.CallbackContext.ReadValue{TValue}"/> で現在値を読み取る。
    /// </param>
    public void OnMove(InputAction.CallbackContext context)
    {
        // 配線サニティチェック: Inspector の UnityEvent 配線スロット入れ違いを早期検知する。
        // 期待アクション名と context.action.name が一致しない場合は警告ログを出すだけで、
        // 値の反映はそのまま続ける(意図的に他アクションを OnMove に流したい用途は無いが、
        // 演習中の動作観察を妨げないため例外を投げず警告に留める)。
        WarnIfWrongAction(context, expectedActionName: "Move");

        // performed → 押下中・スティック傾き中。canceled → 入力解除時(0 が入る)。
        // ReadValue は両方のフェーズで適切な値を返すので、ここでフェーズ分岐は不要。
        _moveInput = context.ReadValue<Vector2>();

        if (!_verboseEventLog)
        {
            return;
        }

        // canceled で 0 ベクトルが来た時もログに出すと観察しやすい。
        Debug.Log(
            $"[{nameof(PlayerControllerEvents)}][{Time.frameCount}] OnMove " +
            $"phase={context.phase} action={context.action?.name} value={_moveInput}",
            this);
    }

    /// <summary>
    /// Player Input コンポーネントの Jump アクション UnityEvent から呼ばれる。
    /// Inspector の Player Input → Events → Player → Jump (PlayerControllerEvents.OnJump) で配線する。
    /// </summary>
    /// <param name="context">
    /// Button アクションは <c>started</c>/<c>performed</c>/<c>canceled</c> の3フェーズで発火する。
    /// 本演習では「押した瞬間」のみ反応させたいため <see cref="InputAction.CallbackContext.performed"/>
    /// で分岐する。
    /// </param>
    public void OnJump(InputAction.CallbackContext context)
    {
        // 配線サニティチェック(配線スロット入れ違い検知)。
        WarnIfWrongAction(context, expectedActionName: "Jump");

        // performed のみで実行(started/canceled で多重発火させない)。
        // ※直接購読版 PlayerController.cs では Jump を実装していないため、
        //   挙動差分そのものが「Player Input 版で何が増えたか」の学習教材になる。
        if (!context.performed)
        {
            return;
        }

        // Rigidbody 非使用のため、視覚確認用に Y 方向へ瞬間オフセット。
        // 重力が無いので落ちてこないが、UnityEvent から呼ばれていることが目視できれば DoD 達成。
        transform.Translate(0f, _jumpImpulse, 0f, Space.World);

        if (!_verboseEventLog)
        {
            return;
        }

        Debug.Log(
            $"[{nameof(PlayerControllerEvents)}][{Time.frameCount}] OnJump performed " +
            $"action={context.action?.name} impulse={_jumpImpulse}",
            this);
    }

    /// <summary>
    /// Player Input コンポーネントの Look アクション UnityEvent から呼ばれる。
    /// Inspector の Player Input → Events → Player → Look (PlayerControllerEvents.OnLook) で配線する。
    /// </summary>
    /// <param name="context">
    /// Look は Mouse delta / Gamepad rightStick から連続値が来るため、
    /// 大きな入力(<see cref="_lookLogThreshold"/> 超過)のみログに出してノイズを抑える。
    /// </param>
    public void OnLook(InputAction.CallbackContext context)
    {
        // 配線サニティチェック(配線スロット入れ違い検知)。
        WarnIfWrongAction(context, expectedActionName: "Look");

        Vector2 look = context.ReadValue<Vector2>();

        // 本演習では Look 入力を回転に反映しない(Phase 2 着手前のため)。
        // UnityEvent が発火していることだけ確認できればよい。
        if (!_verboseEventLog)
        {
            return;
        }

        if (look.sqrMagnitude < _lookLogThreshold * _lookLogThreshold)
        {
            return;
        }

        Debug.Log(
            $"[{nameof(PlayerControllerEvents)}][{Time.frameCount}] OnLook " +
            $"action={context.action?.name} value={look}",
            this);
    }

    // --- helpers ------------------------------------------------------------------

    /// <summary>
    /// Inspector 配線スロットの入れ違い(Move スロットに OnJump を配線、等)を検出するためのガード。
    /// <para>
    /// Player Input の Invoke Unity Events モードは、各 ActionEvent スロット(Move/Jump/Look)に
    /// 対して個別にメソッドを配線する。スロットを取り違えても Unity Editor は警告を出さないため、
    /// 実行時に <c>context.action.name</c> と期待アクション名を突合して警告を出す。
    /// </para>
    /// <para>
    /// 2026-05-12 のオーナー検証で「WASD で上に飛ぶ」「マウスで位置移動する」という不具合が出た
    /// 直接的原因は、まさにこのスロット入れ違い(Move スロット→OnJump、Jump スロット→OnLook、
    /// Look スロット→OnMove)だった。再発防止のための static guard。
    /// </para>
    /// </summary>
    private void WarnIfWrongAction(InputAction.CallbackContext context, string expectedActionName)
    {
        InputAction action = context.action;
        if (action == null)
        {
            return;
        }

        if (string.Equals(action.name, expectedActionName, System.StringComparison.Ordinal))
        {
            return;
        }

        // 同一フレームで連続発火する Look 入力でログが溢れないよう、warn は performed/started のみ。
        if (context.phase != InputActionPhase.Performed && context.phase != InputActionPhase.Started)
        {
            return;
        }

        Debug.LogWarning(
            $"[{nameof(PlayerControllerEvents)}] UnityEvent 配線スロット入れ違いの疑い: " +
            $"expected action='{expectedActionName}' but got '{action.name}'. " +
            "PlayerInput の Events で正しいスロットに配線し直してください。",
            this);
    }
}
