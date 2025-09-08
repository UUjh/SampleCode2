using UnityEngine;

namespace Core.Base
{
    /// <summary>
    /// 간단한 DontDestroyOnLoad 싱글톤 베이스
    /// - 첫 인스턴스를 전역 Instance로 유지, 중복 생성 시 파괴
    /// - 파생 클래스는 OnSingletonAwake에서 초기화 훅 사용
    /// </summary>
    public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T _instance;
        private static bool _isInitialized;

        public static T Instance
        {
            get
            {
                if (!_isInitialized)
                {
                    _instance = Object.FindFirstObjectByType<T>();
                    _isInitialized = true;
                }
                return _instance;
            }
        }

        protected virtual void Awake()
        {
            if (_instance == null)
            {
                _instance = this as T;
                _isInitialized = true;
                DontDestroyOnLoad(gameObject);
                OnSingletonAwake();
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        protected virtual void Start()
        {
            
        }

        protected virtual void Update()
        {
            
        }

        protected virtual void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
                _isInitialized = false;
            }
        }

        protected virtual void OnSingletonAwake() { }
    }
}