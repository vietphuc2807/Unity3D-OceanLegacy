using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pool dùng chung cho mọi ParticleSystem "bắn 1 lần rồi biến mất" (bụi chân,
/// tia lửa, splash nước, v.v). Không cần đặt sẵn trong scene — Instance sẽ
/// tự tạo lazy khi lần đầu được gọi tới.
///
/// Hỗ trợ nhiều prefab khác nhau cùng lúc (vd: bụi đất, bụi cát, bụi tuyết),
/// mỗi prefab có pool riêng, key theo chính prefab đó.
/// </summary>
public sealed class FootstepDustPool : MonoBehaviour
{
    static FootstepDustPool _instance;
    public static FootstepDustPool Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("FootstepDustPool");
                _instance = go.AddComponent<FootstepDustPool>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    [SerializeField] int _prewarmCountPerPrefab = 8;

    // Mỗi prefab có 1 queue instance riêng
    readonly Dictionary<ParticleSystem, Queue<ParticleSystem>> _pools = new();

    // Container con theo prefab, chỉ để gọn Hierarchy khi debug
    readonly Dictionary<ParticleSystem, Transform> _containers = new();

    public ParticleSystem Get(ParticleSystem prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
            return null;

        var queue = GetOrCreateQueue(prefab);

        ParticleSystem instance = queue.Count > 0
            ? queue.Dequeue()
            : CreateInstance(prefab);

        Transform t = instance.transform;
        t.SetPositionAndRotation(position, rotation);
        instance.gameObject.SetActive(true);
        instance.Play(true);

        StartCoroutine(ReturnWhenFinished(prefab, instance));

        return instance;
    }

    Queue<ParticleSystem> GetOrCreateQueue(ParticleSystem prefab)
    {
        if (_pools.TryGetValue(prefab, out var queue))
            return queue;

        queue = new Queue<ParticleSystem>();
        _pools[prefab] = queue;

        var container = new GameObject($"Pool_{prefab.name}").transform;
        container.SetParent(transform);
        _containers[prefab] = container;

        for (int i = 0; i < _prewarmCountPerPrefab; i++)
        {
            var inst = CreateInstance(prefab);
            inst.gameObject.SetActive(false);
            queue.Enqueue(inst);
        }

        return queue;
    }

    ParticleSystem CreateInstance(ParticleSystem prefab)
    {
        var instance = Instantiate(prefab, _containers.TryGetValue(prefab, out var c) ? c : transform);
        return instance;
    }

    void Return(ParticleSystem prefab, ParticleSystem instance)
    {
        if (instance == null)
            return;

        instance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        instance.gameObject.SetActive(false);
        instance.transform.SetParent(_containers.TryGetValue(prefab, out var c) ? c : transform);

        GetOrCreateQueue(prefab).Enqueue(instance);
    }

    // Chờ tới khi particle (và mọi sub-emitter con) phát xong thật sự rồi mới trả về pool.
    // Dùng IsAlive thay vì Invoke(fixedDelay) để không phụ thuộc việc chỉnh
    // đúng "Stop Action" trong prefab, và không trả về pool sớm/muộn sai lệch.
    IEnumerator ReturnWhenFinished(ParticleSystem prefab, ParticleSystem instance)
    {
        yield return null; // chờ 1 frame để Play() kịp áp dụng trước khi check IsAlive

        while (instance != null && instance.IsAlive(true))
            yield return null;

        Return(prefab, instance);
    }
}