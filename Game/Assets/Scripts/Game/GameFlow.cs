using System.Collections.Generic;
using UnityEngine;
using TrustNoWall.Core;

namespace TrustNoWall.Game
{
    /// <summary>
    /// Owns the run state machine per the spec's Flow section: Title, IntroCard, Playing, Dying,
    /// LevelComplete, Paused. Generates each level via <see cref="LevelGenerator.Generate"/>,
    /// steps the sim while Playing, and drives the maze/player views, SFX and effects directly off
    /// sim events and its own transitions. <see cref="Hud"/> reads this class's public state to draw.
    /// </summary>
    public sealed class GameFlow : MonoBehaviour
    {
        public enum State
        {
            Title,
            IntroCard,
            Playing,
            Dying,
            LevelComplete,
            Paused
        }

        private const string BestLevelKey = "TrustNoWall.BestLevel";
        private const float DyingDuration = 0.9f;
        private const float CompleteDuration = 1.5f;
        private const float IntroMaxDuration = 4f;
        private const float MaxDeltaTime = 0.1f;

        // The sim is stepped in chunks of at most this size, re-reading the input source for each
        // chunk, so the autopilot's timing stays accurate even when the frame rate drops.
        private const float MaxInputChunk = 1f / 60f;

        private Camera _camera;
        private Transform _cameraRig;
        private MazeView _mazeView;
        private PlayerView _playerView;
        private Effects _effects;
        private Sfx _sfx;
        private IDirectionSource _input;

        private LevelSim _sim;
        private int _runSeed;

        private float _introTimer;
        private float _dyingTimer;
        private float _completeTimer;

        public State CurrentState { get; private set; } = State.Title;
        public int CurrentLevel { get; private set; }
        public LevelLayout Layout => _sim?.Layout;
        public LevelSim Sim => _sim;
        public int LevelDeaths { get; private set; }
        public float TotalRunTime { get; private set; }
        public int BestLevel { get; private set; }
        public string DeathCause { get; private set; }

        /// <summary>True while demo mode drives the input (shown as a DEMO tag in the HUD).</summary>
        public bool DemoActive { get; private set; }

        /// <summary>Swaps the input source (keyboard or autopilot); used by <see cref="DemoMode"/>.</summary>
        public void SetInput(IDirectionSource input, bool demo)
        {
            _input = input;
            DemoActive = demo;
        }

        public void Configure(
            Camera camera, Transform cameraRig, MazeView mazeView, PlayerView playerView, Effects effects, Sfx sfx, IDirectionSource input)
        {
            _camera = camera;
            _cameraRig = cameraRig;
            _mazeView = mazeView;
            _playerView = playerView;
            _effects = effects;
            _sfx = sfx;
            _input = input;

            _mazeView.SetCamera(camera);
            _effects.SetCamera(camera);
        }

        private void Awake()
        {
            BestLevel = PlayerPrefs.GetInt(BestLevelKey, 0);
        }

        private void Update()
        {
            if (CurrentState != State.Title && CurrentState != State.Paused)
            {
                TotalRunTime += Time.deltaTime;
            }

            switch (CurrentState)
            {
                case State.Title:
                    TickTitle();
                    break;
                case State.IntroCard:
                    TickIntroCard();
                    break;
                case State.Playing:
                    TickPlaying();
                    break;
                case State.Dying:
                    TickDying();
                    break;
                case State.LevelComplete:
                    TickLevelComplete();
                    break;
                case State.Paused:
                    TickPaused();
                    break;
            }

            // Hidden debug key: jump straight to the next level. Kept in the shipped build to make
            // manual testing (and the user's own playtesting) fast.
            if (Input.GetKeyDown(KeyCode.F9))
            {
                SkipLevel();
            }

            if (_sim != null)
            {
                _mazeView.Sync(_sim);
                _playerView.Sync(Time.deltaTime);
                CameraFit.Apply(_camera, _cameraRig, _sim.Layout.N);
            }
        }

        private void TickTitle()
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                StartRun();
            }
        }

        private void TickIntroCard()
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                RestartAttempt();
                return;
            }

            _introTimer += Time.deltaTime;
            if (_introTimer >= IntroMaxDuration || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                CurrentState = State.Playing;
            }
        }

        /// <summary>Dismisses the intro card (as Space would); used by <see cref="DemoMode"/>.</summary>
        public void DismissIntro()
        {
            if (CurrentState == State.IntroCard)
            {
                CurrentState = State.Playing;
            }
        }

        /// <summary>
        /// Jumps straight to the next level (the hidden F9 debug key, also used by
        /// <see cref="DemoMode"/> if the autopilot keeps dying on one level). No-op on the Title or
        /// while paused.
        /// </summary>
        public void SkipLevel()
        {
            if (_sim != null && CurrentState != State.Title && CurrentState != State.Paused)
            {
                BuildLevel(CurrentLevel + 1);
            }
        }

        private void TickPlaying()
        {
            if (Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.Escape))
            {
                CurrentState = State.Paused;
                return;
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                RestartAttempt();
                return;
            }

            float dt = Mathf.Min(Time.deltaTime, MaxDeltaTime);
            int chunks = Mathf.Max(1, Mathf.CeilToInt(dt / MaxInputChunk - 1e-4f));
            float chunk = dt / chunks;
            for (int c = 0; c < chunks && _sim.Status == SimStatus.Playing; c++)
            {
                Dir? held = _input?.GetHeldDirection();
                IReadOnlyList<SimEvent> events = _sim.Step(chunk, held);
                for (int i = 0; i < events.Count; i++)
                {
                    HandleEvent(events[i]);
                }
            }

            if (_sim.Status == SimStatus.Dead)
            {
                EnterDying();
            }
            else if (_sim.Status == SimStatus.Complete)
            {
                EnterLevelComplete();
            }
        }

        private void TickDying()
        {
            _dyingTimer += Time.deltaTime;
            if (_dyingTimer >= DyingDuration)
            {
                _sim.Reset();
                _playerView.Attach(_sim);
                CurrentState = State.Playing;
            }
        }

        private void TickLevelComplete()
        {
            _completeTimer += Time.deltaTime;
            if (_completeTimer >= CompleteDuration)
            {
                BuildLevel(CurrentLevel + 1);
            }
        }

        private void TickPaused()
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                RestartAttempt();
                return;
            }

            if (Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.Escape))
            {
                CurrentState = State.Playing;
            }
        }

        private void RestartAttempt()
        {
            _sim.Reset();
            _playerView.Attach(_sim);
            _introTimer = 0f;
            CurrentState = State.Playing;
        }

        /// <summary>Starts a new run from the Title (Space/Enter, or <see cref="DemoMode"/>'s auto-start).</summary>
        public void StartRun()
        {
            if (CurrentState != State.Title)
            {
                return;
            }

            _runSeed = new System.Random().Next();
            TotalRunTime = 0f;
            BuildLevel(1);
        }

        private void BuildLevel(int level)
        {
            CurrentLevel = level;
            LevelLayout layout = LevelGenerator.Generate(level, (_runSeed * 1000) + level);
            _sim = new LevelSim(layout);
            _mazeView.Build(_sim);
            _playerView.Attach(_sim);
            CameraFit.Apply(_camera, _cameraRig, layout.N);
            LevelDeaths = 0;

            if (layout.NewMechanics.Count > 0)
            {
                _introTimer = 0f;
                CurrentState = State.IntroCard;
            }
            else
            {
                CurrentState = State.Playing;
            }
        }

        private void EnterDying()
        {
            DeathCause = _sim.DeathCause;
            LevelDeaths++;
            _dyingTimer = 0f;
            CurrentState = State.Dying;
            _playerView.TriggerDeath();
            _sfx.PlayDeath();
            _effects.Shake(0.3f, 0.12f);
        }

        private void EnterLevelComplete()
        {
            _completeTimer = 0f;
            CurrentState = State.LevelComplete;
            _effects.Burst(WorldCenter(_sim.Layout.Destination), Palette.Destination, 18, 4.5f, 0.6f, 0.2f);
            _sfx.PlayComplete();

            if (CurrentLevel > BestLevel)
            {
                BestLevel = CurrentLevel;
                PlayerPrefs.SetInt(BestLevelKey, BestLevel);
                PlayerPrefs.Save();
            }
        }

        private void HandleEvent(SimEvent e)
        {
            switch (e.Kind)
            {
                case SimEventKind.Step:
                    _sfx.PlayStep();
                    break;
                case SimEventKind.Bump:
                    _sfx.PlayBump();
                    break;
                case SimEventKind.Teleported:
                    _sfx.PlayTeleport();
                    _effects.Sparkle(WorldCenter(e.Cell), Palette.TeleporterPad);
                    break;
                case SimEventKind.TriggerToggled:
                    _sfx.PlayTrigger();
                    break;
                case SimEventKind.MemoryRevealed:
                    _sfx.PlayMemory();
                    _effects.Sparkle(WorldCenter(e.Cell), Palette.MemoryTile);
                    break;
                case SimEventKind.TileCollapsed:
                    _effects.Dust(WorldCenter(e.Cell));
                    break;
                case SimEventKind.DecoyFound:
                    _effects.Burst(WorldCenter(e.Cell), Palette.Decoy, 14, 4f, 0.5f, 0.18f);
                    break;
                case SimEventKind.Warning:
                    _sfx.PlayWarning();
                    break;
                case SimEventKind.ChaserSpawned:
                    _sfx.PlayChaserSpawn();
                    break;
            }
        }

        private Vector2 WorldCenter(Vector2Int cell) => Edge.CellCenter(cell, _sim.Layout.N);
    }
}
