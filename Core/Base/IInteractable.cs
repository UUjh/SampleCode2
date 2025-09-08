using UnityEngine;

namespace Core.Base
{
    public interface IInteractable
    {
        /// <summary>
        /// 상호작용 가능한지 여부
        /// </summary>
        bool IsInteractable { get; }

        /// <summary>
        /// 상호작용 가능 범위에 들어왔을 때
        /// </summary>
        void OnInteractableEnter();

        /// <summary>
        /// 상호작용 가능 범위에서 나갔을 때
        /// </summary>
        void OnInteractableExit();

        /// <summary>
        /// 상호작용 가능한지 확인
        /// </summary>
        bool CanInteract();
    }
}
