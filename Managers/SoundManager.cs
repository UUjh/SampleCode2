using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Core.Base;
using Core.Data;
using Character.Data;
using Character.States;
using NAudio.CoreAudioApi;
using System;
using Debug = UnityEngine.Debug;
using Objects;

namespace Core.Managers
{
    /// <summary>
    /// 게임 내 사운드 매니저
    /// - BGM/SFX 재생 및 볼륨 관리
    /// - NAudio를 이용한 시스템 오디오 감지로 외부 소리 반응 처리
    /// </summary>
    public class SoundManager : Singleton<SoundManager>
    {
        [Header("Audio Sources")]
        [SerializeField] private AudioSource _bgmSource;
        [SerializeField] private AudioClip _bgmClip;
        [SerializeField] private AudioSource[] _sfxPool;

        [Header("Common SFX")]


        [Header("Default Volumes")]
        // ...

        private float _bgmVolume = 1f;
        private float _sfxVolume = 1f;
        private Dictionary<string, SoundInfo> _activeSounds = new Dictionary<string, SoundInfo>();
        
        // 외부 소리 감지 코루틴 제어
        private Coroutine _externalAudioCo;

        private class SoundInfo
        {
            public AudioSource source;
            public Coroutine delayCoroutine;
            public float baseVolume;
        }

        protected override void Awake()
        {
            base.Awake();
            InitSoundSystem();
            if (DataManager.Instance != null)
            {
                UpdateVolumes(DataManager.Instance.SoundSettings);
                DataManager.Instance.onSoundSettingsChanged += UpdateVolumes;
                PlayBGM(_bgmClip);
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (DataManager.Instance != null)
            {
                DataManager.Instance.onSoundSettingsChanged -= UpdateVolumes;
            }
            StopAllSounds();
            StopExternalAudioCheck();
            _deviceEnumerator?.Dispose();
        }

        private void UpdateVolumes(SoundSettings settings)
        {
            _bgmVolume = settings.bgmVolume;
            _sfxVolume = settings.sfxVolume;

            // BGM 볼륨 업데이트
            _bgmSource.volume = _defBGMVolume * _bgmVolume;

            // BGM 재생/정지 처리
            if (Mathf.Approximately(_bgmVolume, 0f))
            {
                // 볼륨이 0이면 정지 (clip은 유지)
                if (_bgmSource.isPlaying)
                    _bgmSource.Stop();
            }
            else
            {
                // 볼륨이 0이 아니고 clip이 있으면 재생
                if (!_bgmSource.isPlaying && _bgmSource.clip != null)
                    _bgmSource.Play();
            }

            // 재생 중인 SFX만 볼륨 갱신
            foreach (var soundInfo in _activeSounds.Values)
            {
                if (soundInfo.source != null && soundInfo.source.isPlaying)
                    soundInfo.source.volume = soundInfo.baseVolume * _sfxVolume;
            }

            // BGM 볼륨 변경 시 외부 소리 감지 코루틴 시작/중지
            if (_bgmVolume <= 0.1f)
            {
                if (_externalAudioCo != null)
                {
                    StopCoroutine(_externalAudioCo);
                    _externalAudioCo = null;
                }

                _externalAudioCo = StartCoroutine(CheckSystemAudioCo());
                
            }
            else
            {
                StopExternalAudioCheck();
            }
        }

        /// <summary>
        /// 활성화된 소파가 있는지 확인
        /// </summary>
        private bool HasActiveSofa()
        {

        }

        /// <summary>
        /// 소파 상태 변경 시 호출 (DataManager에서 호출)
        /// </summary>
        public void OnSofaStateChanged(bool isActive)
        {
            if (isActive && _bgmVolume <= 0.1f)
            {
                // 소파 활성화되고 BGM 볼륨이 낮으면 외부 소리 감지 시작
                if (_externalAudioCo == null)
                {
                    _externalAudioCo = StartCoroutine(CheckSystemAudioCo());
                }
            }
            else
            {
                // 소파 비활성화되면 외부 소리 감지 중지
                StopExternalAudioCheck();
            }
        }

        private void StopExternalAudioCheck() { }

        private void InitSoundSystem()
        {
            // SFX 풀 초기화
            if (_sfxPool == null || _sfxPool.Length == 0)
            {
                var sfxParent = new GameObject("SFX_Pool").transform;
                sfxParent.SetParent(transform);

                _sfxPool = new AudioSource[10];
                for (int i = 0; i < _sfxPool.Length; i++)
                {
                    var obj = new GameObject($"SFX_{i}");
                    obj.transform.SetParent(sfxParent);
                    var source = obj.AddComponent<AudioSource>();
                    source.playOnAwake = false;
                    source.loop = false;
                    _sfxPool[i] = source;
                    obj.SetActive(false);
                }
            }
        }

        public void PlayBGM(AudioClip clip)
        {
            if (clip == null) return;
            
            _bgmSource.clip = clip;
            _bgmSource.loop = true;
            _bgmSource.volume = _defBGMVolume * _bgmVolume;
            
            // 볼륨이 0이 아니면 재생
            if (!Mathf.Approximately(_bgmVolume, 0f))
            {
                _bgmSource.Play();
            }
        }

        public void PlaySFX(AudioClip clip, float baseVolume = 1f, bool loop = false, string soundId = null, float delay = 0f)
        {
            if (clip == null) return;
            if (Mathf.Approximately(_sfxVolume, 0f)) return;  // 사용자가 효과음 볼륨을 0으로 설정한 경우

            // 이미 재생 중인 사운드가 있으면 중지
            StopSound(soundId);

            // 새로운 사운드 정보 생성
            var soundInfo = new SoundInfo { baseVolume = baseVolume };

            if (delay > 0f)
            {
                soundInfo.delayCoroutine = StartCoroutine(PlayDelayedSound(clip, soundInfo, loop, delay));
            }
            else
            {
                PlaySound(clip, soundInfo, loop);
            }

            // ID가 있는 경우 관리 목록에 추가
            if (!string.IsNullOrEmpty(soundId))
            {
                _activeSounds[soundId] = soundInfo;
            }
        }

        private void PlaySound(AudioClip clip, SoundInfo soundInfo, bool loop)
        {
            // 사용 가능한 AudioSource 찾기
            var source = GetAvailableSource();
            if (source == null) return;

            // 소스 설정 및 재생
            source.clip = clip;
            source.loop = loop;
            source.volume = soundInfo.baseVolume * _sfxVolume;
            source.gameObject.SetActive(true);
            source.Play();

            soundInfo.source = source;

            // 루프가 아닌 경우 재생 완료 후 정리
            if (!loop)
            {
                StartCoroutine(CleanupAfterPlay(source, soundInfo));
            }
        }

        private IEnumerator PlayDelayedSound(AudioClip clip, SoundInfo soundInfo, bool loop, float delay)
        {
            yield return new WaitForSeconds(delay);
            soundInfo.delayCoroutine = null;
            PlaySound(clip, soundInfo, loop);
        }

        private AudioSource GetAvailableSource()
        {
            // LINQ 대신 일반 for 루프 사용으로 성능 향상
            for (int i = 0; i < _sfxPool.Length; i++)
            {
                if (!_sfxPool[i].gameObject.activeSelf)
                {
                    return _sfxPool[i];
                }
            }
            return _sfxPool[0];
        }

        private IEnumerator CleanupAfterPlay(AudioSource source, SoundInfo soundInfo)
        {
            yield return new WaitForSeconds(source.clip.length);
            CleanupSound(soundInfo);
        }

        private void StopSound(string soundId)
        {
            if (string.IsNullOrEmpty(soundId)) return;
            if (_activeSounds.TryGetValue(soundId, out var soundInfo))
            {
                CleanupSound(soundInfo);
                _activeSounds.Remove(soundId);
            }
        }
        public void StopSFX(string soundId)
        {
            StopSound(soundId);
        }

        private void CleanupSound(SoundInfo soundInfo)
        {
            if (soundInfo.delayCoroutine != null)
            {
                StopCoroutine(soundInfo.delayCoroutine);
            }
            if (soundInfo.source != null)
            {
                soundInfo.source.Stop();
                soundInfo.source.gameObject.SetActive(false);
            }
        }

        public void StopAllSounds()
        {
            foreach (var soundInfo in _activeSounds.Values)
            {
                CleanupSound(soundInfo);
            }
            _activeSounds.Clear();
        }

        #region Character Sounds
       
        #endregion


        #region Volume
        public void SetSFXVolume(float value)
        {
            if (DataManager.Instance != null)
            {
                var settings = DataManager.Instance.SoundSettings;
                settings.sfxVolume = Mathf.Clamp01(value);
                DataManager.Instance.UpdateSoundSettings(settings);
            }
        }

        public void SetBGMVolume(float value)
        {
            if (DataManager.Instance != null)
            {
                var settings = DataManager.Instance.SoundSettings;
                settings.bgmVolume = Mathf.Clamp01(value);
                DataManager.Instance.UpdateSoundSettings(settings);
            }
        }
        #endregion


        #region System Audio Detection
        private MMDeviceEnumerator _deviceEnumerator;
        private MMDevice _defDevice;
        private string _lastDeviceId = ""; // 장치 ID 추적용
        private WaitForSeconds _audioCheckDelay = new WaitForSeconds(3f); // 3초마다 체크
        private WaitForSeconds _fastCheckDelay = new WaitForSeconds(1f); // 1초마다 체크
        private bool _isExternalSoundDetected = false;
        private int _externalSoundCount = 0;
        private int _externalSoundOffCount = 0; // 외부 소리 중단 카운터
        private const int EXTERNAL_SOUND_THRESHOLD = 5; // 5번 연속 감지 시 실행
        
        // 순간적인 큰 소리 감지용
        private bool _isLoudSoundDetected = false;
        private const float LOUD_SOUND_THRESHOLD = 0.85f; // 0.85 이상이면 큰 소리로 판단
        
        // 성능 최적화를 위한 캐싱
        private float _lastPeakValue = 0f;
        private float _peakValueCacheTime = 0f;
        private const float PEAK_CACHE_DURATION = 0.5f; // 0.5초 캐시
        
        // 외부 사운드 상태 변화 이벤트
        public Action<bool> OnExternalSoundStateChanged;
        
        // 큰 소리 감지 이벤트
        public Action OnLoudSoundDetected;

        /// <summary>
        /// 외부 사운드가 현재 감지되고 있는지 여부
        /// </summary>
        public bool IsExternalSoundDetected => _isExternalSoundDetected;
        
        /// <summary>
        /// 큰 소리가 현재 감지되고 있는지 여부
        /// </summary>
        public bool IsLoudSoundDetected => _isLoudSoundDetected;

        /// <summary>
        /// 장치가 바뀌었을 때만 갱신
        /// </summary>
        private void ChkRefreshDefaultDevice()
        {
            // 현재 기본 장치 ID 확인
            string currentDeviceId = "";
            try
            {
                var currentDevice = _deviceEnumerator?.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                currentDeviceId = currentDevice?.ID ?? "";
            }
            catch { }

            // 장치가 바뀌었을 때만 갱신
            if (currentDeviceId != _lastDeviceId)
            {
                RefreshDefaultDevice();
                _lastDeviceId = currentDeviceId;
            }
        }

        /// <summary>
        /// 기본 장치 갱신
        /// </summary>
        private void RefreshDefaultDevice()
        {
            
        }

        /// <summary>
        /// 외부 소리가 플레이되고 있는지 체크
        /// BGM 볼륨이 0.1 이하이고 NAudio의 MasterPeakValue가 0.05 이상일 때 true 반환
        /// </summary>
        public bool IsExternalSoundPlaying()
        {
            // 게임 사운드가 조용한지 (BGM이 없거나 아주 작음.)
            bool isBGMLow = _bgmVolume <= 0.1f;

            // 장치 갱신 체크
            ChkRefreshDefaultDevice();

            // 캐시된 NAudio MasterPeakValue 사용
            bool isSystemAudioHigh = false;
            try
            {
                if (_defDevice != null)
                {
                    float peakValue = GetCachedPeakValue();
                    isSystemAudioHigh = peakValue >= 0.05f;
                    
                    // 디버그 로그 추가
                    Debug.Log($"[외부소리체크] 시스템피크: {peakValue:F3}, 시스템높음: {isSystemAudioHigh}");
                }
                else
                {
                    Debug.LogWarning("[외부소리체크] _defDevice가 null입니다! NAudio 초기화 필요");
                    // NAudio 재초기화 시도
                    ReinitializeNAudio();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"외부 소리 체크 중 오류 발생: {ex.Message}");
                return false;
            }
            
            // 시스템 오디오가 크고, 게임은 조용할 때 외부 소리로 판단
            bool result = isSystemAudioHigh && isBGMLow;
            Debug.Log($"[외부소리체크] 최종결과: {result} (시스템높음:{isSystemAudioHigh} && BGM낮음:{isBGMLow})");
            return result;
        }

        /// <summary>
        /// 캐시된 시스템 오디오 피크값 반환 (성능 최적화)
        /// </summary>
        private float GetCachedPeakValue()
        {
            if (Time.time - _peakValueCacheTime > PEAK_CACHE_DURATION)
            {
                _lastPeakValue = _defDevice.AudioMeterInformation.MasterPeakValue;
                _peakValueCacheTime = Time.time;
            }
            return _lastPeakValue;
        }

        /// <summary>
        /// 큰 소리 감지 체크 (Update에서 호출)
        /// </summary>
        private void ChkLoudSound()
        {
            // 장치가 없거나 이미 감지 중이면 체크하지 않음
            if (_defDevice == null || _isLoudSoundDetected) return;

            try
            {
                // 현재 peak 값 가져오기
                float currentPeakValue = _defDevice.AudioMeterInformation.MasterPeakValue;
                bool isLoudSound = currentPeakValue >= LOUD_SOUND_THRESHOLD;
                
                if (isLoudSound)
                {
                    _isLoudSoundDetected = true;
                    OnLoudSoundDetected?.Invoke();
                    
                    
                    // 쿨다운 코루틴 시작
                    StartCoroutine(LoudSoundCooldownCo());
                }
            }
            catch (Exception ex)
            {

            }
        }
        

        /// <summary>
        /// NAudio 재초기화 시도
        /// </summary>
        private void ReinitializeNAudio()
        {
            try
            {
                _deviceEnumerator?.Dispose();
                _deviceEnumerator = new MMDeviceEnumerator();
                RefreshDefaultDevice();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[외부소리체크] NAudio 재초기화 실패: {ex.Message}");
            }
        }

        private IEnumerator CheckSystemAudioCo()
        {
            while (true)
            {
                // 게임이 일시정지된 경우 체크 중단
                if (Time.timeScale <= 0f)
                {
                    yield return _audioCheckDelay;
                    continue;
                }

                yield return _audioCheckDelay;

                // 소파가 비활성화된 경우 코루틴 중단
                if (!HasActiveSofa())
                {
                    StopExternalAudioCheck();
                    yield break;
                }

                if (_deviceEnumerator == null)
                {
                    yield return _audioCheckDelay;

                    try
                    {
                        _deviceEnumerator = new MMDeviceEnumerator();
                        RefreshDefaultDevice();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError("StartSystemAudioCheck 오류가 발생했습니다. " + ex.Message);
                    }

                    continue;
                }

                // 장치 갱신 체크
                ChkRefreshDefaultDevice();

                // 외부 소리 체크
                bool isExternalSound = IsExternalSoundPlaying();
                
                if (isExternalSound)
                {
                    // 외부 소리가 감지됨
                    _externalSoundOffCount = 0; // 중단 카운터 리셋
                    
                    if (!_isExternalSoundDetected)
                    {
                        // 처음 외부 소리가 감지되면 카운터 시작
                        _isExternalSoundDetected = true;
                        _externalSoundCount = 1;
                        OnExternalSoundStateChanged?.Invoke(true); // 이벤트 발생
                    }
                    else
                    {
                        // 이미 감지 중이면 카운터 증가
                        _externalSoundCount++;
                        
                        // 5번 연속 감지되면 모든 캐릭터의 GoSofa 실행
                        if (_externalSoundCount >= EXTERNAL_SOUND_THRESHOLD)
                        {
                            GoSofaAllCharacters();
                            _externalSoundCount = 0; // 리셋
                        }
                    }
                    
                    // 소파 상태에 따른 체크 주기 결정 
                    yield return GetOptimizedCheckDelay();
                }
                else
                {
                    // 외부 소리가 감지되지 않음
                    _externalSoundOffCount++;
                    
                    if (_externalSoundOffCount >= EXTERNAL_SOUND_THRESHOLD)
                    {
                        // 3번 연속으로 중단되면 완전히 중단된 것으로 판단
                        if (_isExternalSoundDetected)
                        {
                            Debug.Log("외부 소리가 완전히 중단되었습니다.");
                            OnExternalSoundStateChanged?.Invoke(false); // 이벤트 발생
                        }
                        _isExternalSoundDetected = false;
                        _externalSoundCount = 0;
                        _externalSoundOffCount = 0;
                    }
                    
                    // 외부 소리가 중단되면 1초마다 체크
                    yield return _fastCheckDelay;
                }
            }
        }

        /// <summary>
        /// 소파 상태에 따른 최적화된 체크 지연 시간 반환
        /// </summary>
        private WaitForSeconds GetOptimizedCheckDelay()
        {
            bool hasCharactersOnSofa = CheckCharactersOnSofa();
            bool hasEmptySlots = CheckEmptySofaSlots();
            
            if (!hasCharactersOnSofa && hasEmptySlots)
            {
                // 캐릭터가 없고 자리가 있으면 2초마다 체크
                return _fastCheckDelay;
            }
            else if (hasCharactersOnSofa && hasEmptySlots)
            {
                // 캐릭터가 있고 자리가 있으면 3초마다 체크
                return _audioCheckDelay;
            }
            else
            {
                // 자리가 없으면 3초마다 체크
                return _audioCheckDelay;
            }
        }

        /// <summary>
        /// 모든 소파에 캐릭터가 있는지 체크
        /// </summary>
        private bool CheckCharactersOnSofa()
        {
            
        }

        /// <summary>
        /// 모든 소파에 빈 자리가 있는지 체크 
        /// </summary>
        private bool CheckEmptySofaSlots()
        {
            
        }

        public void GoSofaAllCharacters()
        {
            // ObjectManager에서 모든 캐릭터를 가져와서 소파로 이동
            if (ObjectManager.Instance != null)
            {
                // 모든 소파의 빈 슬롯 수집 
                var availableSlots = new List<(Sofa sofa, int slotIndex)>();
                
                var sofas = ObjectManager.Instance.Sofas;
                foreach (var sofa in sofas)
                {
                    if (sofa == null || !sofa.gameObject.activeInHierarchy || sofa.sofaData == null)
                        continue;
                        
                    // 이 소파의 빈 슬롯들 수집
                    for (int i = 0; i < sofa.sofaData.sitPositions.Length; i++)
                    {
                        if (!sofa.IsUsingSlot(i))
                        {
                            availableSlots.Add((sofa, i));
                        }
                    }
                }
                
                // 빈 슬롯이 없으면 아무것도 하지 않음
                if (availableSlots.Count <= 0)
                {
                    return;
                }
                
                // 슬롯을 무작위로 섞어서 공평하게 배치
                Utils.Shuffle(availableSlots);

                // 빈 슬롯이 있을 때만 캐릭터들을 이동
                int movedCharacters = 0;
                var characters = new List<Character.Controller.CharacterController>(ObjectManager.Instance.Characters);
                Utils.Shuffle(characters);
                foreach (var character in characters)
                {
                    if (character != null && character.CurState != null && availableSlots.Count > 0)
                    {
                        
                        if (!character.CurSofaSlot.HasValue && // (...) // 소파 사용 중이지 않은 캐릭터만
                            )
                        {
                            // 사용 가능한 슬롯 중 첫 번째 가져오기
                            var (targetSofa, slotIndex) = availableSlots[0];
                            availableSlots.RemoveAt(0);
                            
                            // 해당 소파를 이미 사용 중이지 않은 캐릭터만 이동
                            if (!targetSofa.IsCharacterUsing(character))
                            {
                                
                            }
                            else
                            {
                                // 이 캐릭터가 해당 소파를 사용 중이면 슬롯을 다시 추가
                                availableSlots.Add((targetSofa, slotIndex));
                            }
                        }
                    }
                }
            }
        }

        #endregion
    }
} 