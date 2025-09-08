using Spine.Unity;
using UnityEngine;

namespace Character.Data
{
    /// <summary>
    /// 캐릭터의 기본 데이터를 저장하는 ScriptableObject
    /// Unity 에디터에서 캐릭터 데이터를 생성하고 관리할 수 있음
    /// </summary>
    [CreateAssetMenu(menuName = "Character/Data", fileName = "NewCharacterData")]
    public class CharacterData : ScriptableObject
    {
        [Header("Basic Info")]
        public int characterId = 0;
        public string characterName;
        public CharacterType characterType;
        public Sprite characterIcon;
        public GameObject characterPrefab;
        public float speed = 3f;
        public float runSpeed = 5f;
        public float jumpHeight = 1f;
        public int clickCoin = 200;
        public int eatCoin = 100;
        public float angeyChance = 0.1f;


        [Header("Shop Info")]
        public int price = 100000;
        public bool isUnlocked = false;
        public int needLicenseId = -1;

        [Header("Animation")]

        [Header("Sound")]
        
        [Header("Initial State")]
        public float hungerThreshold = 300f;    // 배고픔 시간 (초)

        [Header("Drag Settings")]
        [Tooltip("Drag Y Offset")]
        public float dragYOffset = -1.5f;        

    }
}