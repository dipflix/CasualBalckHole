using System;
using System.Collections;
using CasualHole.Audio.Context;
using CasualHole.Audio.Services;
using CasualHole.Game.Camera.Interface;
using CasualHole.Game.Hole.Interface;
using CasualHole.Game.Services;
using CasualHole.Game.UI.Interface;
using CasualHole.Game.UI.Score.Interface;
using CasualHole.Levels.Interface;
using CasualHole.Services;
using CasualHole.Setting.Interface;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

namespace CasualHole.Game.GameProcess
{
    public class GameProcessService : BaseBehaviourService, IGameProcessService
    {
        private IAudioService _audioService;
        private IUIGameService _uiGameService;
        private IScoreService _scoreService;
        private ISettingService _settingService;
        private ILevelService _levelService;

        private IHoleBehaviour _holeBehaviour;
        
        private AudioGameContext _audioGameContext;

        private ICameraBehaviour _cameraBehaviour;

        private GameProcessState _gameProcessState;

        [Inject]
        private void Construct(
            IAudioService audioService,
            IUIGameService uiGameService,
            IScoreService scoreService,
            ICameraBehaviour cameraBehaviour,
            ISettingService settingService,
            ILevelService levelService,
            IHoleBehaviour holeBehaviour,
            GameProcessState gameProcessState,
            AudioGameContext audioGameContext)
        {
            _audioService = audioService;
            _audioGameContext = audioGameContext;
            _gameProcessState = gameProcessState;
            _uiGameService = uiGameService;
            _scoreService = scoreService;
            _cameraBehaviour = cameraBehaviour;
            _settingService = settingService;
            _levelService = levelService;
            _holeBehaviour = holeBehaviour;
            
            InitializeComponents();
            Initialize();
        }

        private void InitializeComponents()
        {
            _scoreService.Initialize();
            _uiGameService.Initialize();
            _audioService.Initialize();
            _settingService.Initialize();
            _levelService.Initialize();
            
            _holeBehaviour.Initialize();
            _holeBehaviour.OnCollectScoreObject += OnCollectScoreTrash;
            _holeBehaviour.OnCollectTrashObject += OnCollectToxicTrash;
        }

        public override void Initialize()
        {
            base.Initialize();
            Time.timeScale = 1;
            _gameProcessState.InitGameState();
            _gameProcessState.CurrentTimerScore
                .Subscribe(time => _scoreService.Timer.SetTime(time));

            _gameProcessState.CurrentScore
                .Subscribe(score => _scoreService.SetScore(score));

            _scoreService.SetTotalScore(_gameProcessState.TotalScore);

            InitGameLogic();
        }

        private void InitGameLogic()
        {
            _audioService.PlayMusic2D(_audioGameContext.BackgroundMusic, true);

            var timer = new CompositeDisposable();

            this.UpdateAsObservable()
                .Where(_ => Input.GetKeyDown(KeyCode.Escape))
                .Subscribe(_ => OnPressBackButton());

            Observable
                .Timer(TimeSpan.FromSeconds(1))
                .Where(_ => !_gameProcessState.GamePaused)
                .Repeat()
                .Subscribe(_ =>
                {
                    var time = _gameProcessState.CurrentTimerScore.Value;

                    _gameProcessState
                        .CurrentTimerScore
                        .SetValueAndForceNotify(time - 1);


                    if (time <= 6 && time > 1)
                    {
                        _scoreService.Timer.TimeIsRunningOut();
                        _audioService.PlaySound2D(
                            _audioGameContext.TimeOutSound[time % _audioGameContext.TimeOutSound.Count]);
                    }
                }).AddTo(timer);

            _gameProcessState.CurrentScore
                .Where(i => i == _gameProcessState.TotalScore)
                .Subscribe(_ =>
                {
                    MainThreadDispatcher.StartCoroutine(OnGameWin());
                    StopAllCoroutines();
                    timer.Dispose();
                });


            _gameProcessState
                .CurrentTimerScore
                .Where(time => time == 0)
                .Subscribe(_ =>
                {
                    _scoreService.Timer.TimeIsEnd();
                    MainThreadDispatcher.StartCoroutine(OnGameLose());
                    timer.Dispose();
                });
        }


        private void OnCollectScoreTrash()
        {
            if (_gameProcessState.GamePaused) return;

            _audioService.PlaySound2D(_audioGameContext.CollectSound);
            _gameProcessState.CurrentScore.SetValueAndForceNotify(_gameProcessState.CurrentScore.Value + 1);
        }

        private void OnCollectToxicTrash()
        {
            if (_gameProcessState.GamePaused) return;

            _audioService.PlaySound2D(_audioGameContext.CollectToxicSound);
            MainThreadDispatcher.StartCoroutine(_cameraBehaviour.ShakeCamera());
            MainThreadDispatcher.StartCoroutine(OnGameLose());
        }

        public IEnumerator OnGameWin()
        {
            _audioService.PlaySound2D(_audioGameContext.WinSound);
            GameProcessPause(true);

            _levelService.SetLevelAsCompleted(SceneManager.GetActiveScene().name);

            yield return new WaitForSeconds(_audioGameContext.WinSound.length / 2);
            _uiGameService.WinWindow.Show();
        }

        public IEnumerator OnGameLose()
        {
            _audioService.PlaySound2D(_audioGameContext.LoseSound);
            GameProcessPause(true);

            yield return new WaitForSeconds(_audioGameContext.LoseSound.length / 2);

            _uiGameService.LoseWindow.Show();
        }
        
        public void GameProcessPause(bool paused)
        {
            _gameProcessState.GamePaused = paused;
        }


        public void OnPressBackButton()
        {
            if (!_uiGameService.GameMenuWindow.IsShown())
            {
                GameProcessPause(true);
                _uiGameService.GameMenuWindow.Show();
            }
            else
            {
                GameProcessPause(false);
                _uiGameService.GameMenuWindow.Hide();
            }
        }
    }
}