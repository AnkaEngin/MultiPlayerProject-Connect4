using System.Collections;
using UnityEngine;

// Animates tokens dropping from above the board to their final position
public class TokenAnimator : MonoBehaviour
{
    [SerializeField] private float dropTime = 0.35f;
    [SerializeField] private AnimationCurve dropCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    private void Reset() // Sets a default gravity curve when component is first added
    {
        dropCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0f),
            new Keyframe(1f, 1f, 2f, 0f));
    }

    public void PlayDrop(SpriteRenderer tokenSR, Sprite sprite,
                         Vector3 startPos, Vector3 targetPos)
    {
        StartCoroutine(AnimateDrop(tokenSR, sprite, startPos, targetPos));
    }

    private IEnumerator AnimateDrop(SpriteRenderer tokenSR, Sprite sprite,
                                     Vector3 startPos, Vector3 targetPos)
    {
        tokenSR.sprite             = sprite;
        tokenSR.transform.position = startPos;

        float elapsed = 0f;
        while (elapsed < dropTime)
        {
            elapsed += Time.deltaTime;
            float t      = Mathf.Clamp01(elapsed / dropTime);
            float curved = dropCurve.Evaluate(t);
            tokenSR.transform.position = Vector3.LerpUnclamped(startPos, targetPos, curved);
            yield return null;
        }

        // Snap to final positon
        tokenSR.transform.position = targetPos;
    }
}
