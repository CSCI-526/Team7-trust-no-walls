using UnityEngine;

namespace TrustNoWall.Game
{
    /// <summary>
    /// Demo mode for the gameplay video: on when the page URL has <c>demo=1</c> (WebGL) or toggled
    /// with F2. While on, <see cref="GameFlow"/> reads input from an <see cref="AutopilotSource"/>
    /// instead of the keyboard, the run auto-starts from the Title after 1.5 s, and intro cards are
    /// dismissed after 1.5 s. If the autopilot keeps dying on one level (a rare level it cannot
    /// solve), the level is skipped so the demo keeps moving. F2 off restores keyboard input.
    /// </summary>
    public sealed class DemoMode : MonoBehaviour
    {
        private const float AutoStartDelay = 1.5f;
        private const float IntroDismissDelay = 1.5f;
        private const int SkipAfterDeaths = 5;

        private GameFlow _flow;
        private IDirectionSource _keyboard;
        private IDirectionSource _autopilot;
        private float _stateTimer;
        private GameFlow.State _lastState;
        private int _lastLevel;

        public bool IsOn { get; private set; }

        public void Configure(GameFlow flow, IDirectionSource keyboard, IDirectionSource autopilot)
        {
            _flow = flow;
            _keyboard = keyboard;
            _autopilot = autopilot;
        }

        private void Start()
        {
            SetOn(UrlRequestsDemo(Application.absoluteURL));
        }

        /// <summary>True if <paramref name="url"/> has a <c>demo=1</c> query parameter.</summary>
        public static bool UrlRequestsDemo(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return false;
            }

            int q = url.IndexOf('?');
            if (q < 0)
            {
                return false;
            }

            int hash = url.IndexOf('#', q);
            string query = hash < 0 ? url.Substring(q + 1) : url.Substring(q + 1, hash - q - 1);
            foreach (string part in query.Split('&'))
            {
                if (part == "demo=1")
                {
                    return true;
                }
            }

            return false;
        }

        private void SetOn(bool on)
        {
            IsOn = on;
            _stateTimer = 0f;
            if (_flow != null)
            {
                _flow.SetInput(on ? _autopilot : _keyboard, on);
            }
        }

        private void Update()
        {
            if (_flow == null)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.F2))
            {
                SetOn(!IsOn);
            }

            if (_flow.CurrentState != _lastState || _flow.CurrentLevel != _lastLevel)
            {
                _lastState = _flow.CurrentState;
                _lastLevel = _flow.CurrentLevel;
                _stateTimer = 0f;
            }

            if (!IsOn)
            {
                return;
            }

            _stateTimer += Time.deltaTime;
            switch (_flow.CurrentState)
            {
                case GameFlow.State.Title:
                    if (_stateTimer >= AutoStartDelay)
                    {
                        _flow.StartRun();
                    }

                    break;
                case GameFlow.State.IntroCard:
                    if (_stateTimer >= IntroDismissDelay)
                    {
                        _flow.DismissIntro();
                    }

                    break;
                case GameFlow.State.Playing:
                    if (_flow.LevelDeaths >= SkipAfterDeaths)
                    {
                        _flow.SkipLevel();
                    }

                    break;
            }
        }
    }
}
