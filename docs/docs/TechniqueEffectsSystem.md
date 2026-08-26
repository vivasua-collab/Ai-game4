# Система эффектов техник

**Создано:** 2026-04-03
**Версия:** 1.0

---

## Обзор

Техники в игре имеют визуальные эффекты, которые:
1. Показывают направление атаки
2. Индикатор стихии (огонь, вода, и т.д.)
3. Область поражения
4. Анимацию "разрастания" (expanding mist, expanding aura)

---

## Типы эффектов

### 1. Направленные (Directional)

Эффекты, которые движутся в определённом направлении:
- `effect_fire_slash.png` — рубящая атака огнём
- `effect_water_wave.png` — волна воды
- `effect_air_blade.png` — воздушный клинок
- `effect_lightning_bolt.png` — молния
- `effect_earth_spike.png` — земляной шип
- `effect_void_rift.png` — разрыв пустоты

### 2. Расширяющиеся (Expanding)

Эффекты, которые разрастаются от центра:
- `effect_mist_expanding.png` — расширяющийся туман
- `effect_poison_cloud.png` — ядовитое облако
- `effect_healing_aura.png` — аура исцеления
- `effect_qi_explosion.png` — взрыв ци

### 3. Статические (Static)

Эффекты, которые остаются на месте:
- `effect_defense_barrier.png` — защитный барьер
- `effect_formation_array.png` — формационный массив

---

## Базовая архитектура

### TechniqueEffect.cs

```csharp
// Создано: 2026-04-03
using UnityEngine;
using System.Collections;

/// <summary>
/// Базовый класс для эффектов техник.
/// </summary>
public abstract class TechniqueEffect : MonoBehaviour
{
    [Header("Base Settings")]
    [SerializeField] protected Element element;
    [SerializeField] protected float duration = 1f;
    [SerializeField] protected AnimationCurve scaleCurve;
    [SerializeField] protected AnimationCurve alphaCurve;

    protected SpriteRenderer spriteRenderer;
    protected float elapsedTime;
    protected Vector3 originalScale;

    public Element Element => element;

    protected virtual void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        originalScale = transform.localScale;
    }

    protected virtual void Update()
    {
        if (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;

            // Применяем кривые анимации
            ApplyScaleAnimation(t);
            ApplyAlphaAnimation(t);

            if (elapsedTime >= duration)
            {
                OnEffectComplete();
            }
        }
    }

    protected virtual void ApplyScaleAnimation(float t)
    {
        if (scaleCurve != null)
        {
            float scale = scaleCurve.Evaluate(t);
            transform.localScale = originalScale * scale;
        }
    }

    protected virtual void ApplyAlphaAnimation(float t)
    {
        if (alphaCurve != null && spriteRenderer != null)
        {
            Color color = spriteRenderer.color;
            color.a = alphaCurve.Evaluate(t);
            spriteRenderer.color = color;
        }
    }

    /// <summary>
    /// Запускает эффект.
    /// </summary>
    public virtual void Play(Vector2 origin, Vector2 direction = default)
    {
        transform.position = origin;
        elapsedTime = 0f;
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Останавливает эффект.
    /// </summary>
    public virtual void Stop()
    {
        gameObject.SetActive(false);
    }

    protected virtual void OnEffectComplete()
    {
        Destroy(gameObject);
    }
}
```

---

## Направленные эффекты

### DirectionalEffect.cs

```csharp
// Создано: 2026-04-03
using UnityEngine;

/// <summary>
/// Эффект, движущийся в определённом направлении.
/// Например: огненный удар, волна воды, воздушный клинок.
/// </summary>
public class DirectionalEffect : TechniqueEffect
{
    [Header("Directional Settings")]
    [SerializeField] private float speed = 10f;
    [SerializeField] private float maxDistance = 5f;
    [SerializeField] private bool penetrateEnemies = false;

    private Vector2 direction;
    private Vector2 startPosition;
    private Collider2D[] hitBuffer = new Collider2D[20];

    public override void Play(Vector2 origin, Vector2 direction = default)
    {
        base.Play(origin, direction);

        this.direction = direction.normalized;
        startPosition = origin;

        // Поворачиваем спрайт в направлении движения
        float angle = Mathf.Atan2(this.direction.y, this.direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    protected override void Update()
    {
        base.Update();

        // Движение в направлении
        transform.position += (Vector3)(direction * speed * Time.deltaTime);

        // Проверка максимальной дистанции
        float distance = Vector2.Distance(startPosition, transform.position);
        if (distance >= maxDistance)
        {
            OnEffectComplete();
            return;
        }

        // Проверка попаданий
        CheckHits();
    }

    private void CheckHits()
    {
        int hitCount = Physics2D.OverlapCircleNonAlloc(
            transform.position,
            0.5f,
            hitBuffer
        );

        for (int i = 0; i < hitCount; i++)
        {
            var target = hitBuffer[i].GetComponent<ICombatTarget>();
            if (target != null && target.IsHostile)
            {
                ApplyDamage(target);

                if (!penetrateEnemies)
                {
                    OnEffectComplete();
                    return;
                }
            }
        }
    }

    private void ApplyDamage(ICombatTarget target)
    {
        DamageInfo damage = new DamageInfo
        {
            Amount = CalculateDamage(),
            Element = element,
            Source = null,
            HitPoint = target.Position
        };

        target.TakeDamage(damage);
    }

    protected virtual int CalculateDamage()
    {
        // Базовый урон, можно переопределить
        return 10;
    }
}
```

---

## Расширяющиеся эффекты

### ExpandingEffect.cs

```csharp
// Создано: 2026-04-03
using UnityEngine;

/// <summary>
/// Эффект, разрастающийся от центра.
/// Например: туман, аура исцеления, ядовитое облако.
/// </summary>
public class ExpandingEffect : TechniqueEffect
{
    [Header("Expanding Settings")]
    [SerializeField] private float startRadius = 0.5f;
    [SerializeField] private float maxRadius = 3f;
    [SerializeField] private LayerMask affectedLayers;
    [SerializeField] private bool applyEffectContinuously = true;
    [SerializeField] private float effectInterval = 0.5f;

    private float currentRadius;
    private float lastEffectTime;
    private Collider2D[] affectedBuffer = new Collider2D[30];

    public override void Play(Vector2 origin, Vector2 direction = default)
    {
        base.Play(origin, direction);
        currentRadius = startRadius;
        lastEffectTime = 0f;
    }

    protected override void ApplyScaleAnimation(float t)
    {
        // Интерполируем радиус
        currentRadius = Mathf.Lerp(startRadius, maxRadius, t);

        // Масштабируем спрайт
        float scale = currentRadius / startRadius;
        transform.localScale = originalScale * scale;
    }

    protected override void Update()
    {
        base.Update();

        // Применяем эффект с интервалом
        if (applyEffectContinuously && Time.time - lastEffectTime >= effectInterval)
        {
            ApplyEffectToTargets();
            lastEffectTime = Time.time;
        }
    }

    private void ApplyEffectToTargets()
    {
        int hitCount = Physics2D.OverlapCircleNonAlloc(
            transform.position,
            currentRadius,
            affectedBuffer,
            affectedLayers
        );

        for (int i = 0; i < hitCount; i++)
        {
            ProcessTarget(affectedBuffer[i]);
        }
    }

    protected virtual void ProcessTarget(Collider2D target)
    {
        // Переопределите в наследниках
        var combatTarget = target.GetComponent<ICombatTarget>();
        if (combatTarget != null)
        {
            ApplyEffect(combatTarget);
        }
    }

    protected virtual void ApplyEffect(ICombatTarget target)
    {
        // Базовая реализация — урон
        switch (element)
        {
            case Element.Poison:
                // Яд наносит урон и накладывает эффект отравления
                ApplyPoison(target);
                break;

            case Element.Neutral:
            default:
                // Исцеление для нейтрального элемента
                if (element == Element.Neutral)
                {
                    ApplyHealing(target);
                }
                break;
        }
    }

    private void ApplyPoison(ICombatTarget target)
    {
        // Наносим урон и накладываем отравление
        DamageInfo damage = new DamageInfo
        {
            Amount = 5,
            Element = Element.Poison,
            Source = null,
            HitPoint = target.Position
        };
        target.TakeDamage(damage);

        // TODO: Добавить статус-эффект отравления
    }

    private void ApplyHealing(ICombatTarget target)
    {
        // Исцеляем союзников
        if (!target.IsHostile)
        {
            // target.Heal(5);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, currentRadius);
    }
}
```

---

## Специализированные эффекты

### MistExpandingEffect.cs

```csharp
// Создано: 2026-04-03
using UnityEngine;

/// <summary>
/// Расширяющийся туман — снижает видимость врагов.
/// </summary>
public class MistExpandingEffect : ExpandingEffect
{
    [Header("Mist Settings")]
    [SerializeField] private float visionReduction = 0.5f;

    protected override void ApplyEffect(ICombatTarget target)
    {
        if (target.IsHostile)
        {
            // Снижаем видимость врага
            // target.ApplyVisionDebuff(visionReduction, duration);
        }
    }
}
```

### FormationArrayEffect.cs

```csharp
// Создано: 2026-04-03
using UnityEngine;

/// <summary>
/// Формационный массив — статический эффект на земле.
/// </summary>
public class FormationArrayEffect : TechniqueEffect
{
    [Header("Formation Settings")]
    [SerializeField] private float radius = 2f;
    [SerializeField] private LayerMask affectedLayers;
    [SerializeField] private float effectInterval = 1f;

    private Collider2D[] affectedBuffer = new Collider2D[30];
    private float lastEffectTime;

    public override void Play(Vector2 origin, Vector2 direction = default)
    {
        base.Play(origin, direction);
        lastEffectTime = 0f;
    }

    protected override void Update()
    {
        base.Update();

        // Применяем эффект с интервалом
        if (Time.time - lastEffectTime >= effectInterval)
        {
            ApplyFormationEffect();
            lastEffectTime = Time.time;
        }
    }

    private void ApplyFormationEffect()
    {
        int hitCount = Physics2D.OverlapCircleNonAlloc(
            transform.position,
            radius,
            affectedBuffer,
            affectedLayers
        );

        for (int i = 0; i < hitCount; i++)
        {
            var target = affectedBuffer[i].GetComponent<ICombatTarget>();
            if (target != null && !target.IsHostile)
            {
                // Усиление союзников в формации
                ApplyBuff(target);
            }
        }
    }

    private void ApplyBuff(ICombatTarget target)
    {
        // Усиление защиты или атаки
        // target.ApplyBuff(BuffType.Defense, 0.2f, effectInterval + 0.1f);
    }
}
```

---

## Фабрика эффектов

### TechniqueEffectFactory.cs

```csharp
// Создано: 2026-04-03
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Фабрика для создания эффектов техник.
/// </summary>
public class TechniqueEffectFactory : MonoBehaviour
{
    [System.Serializable]
    public class EffectPool
    {
        public EffectType type;
        public GameObject prefab;
        public int initialPoolSize = 5;
    }

    public enum EffectType
    {
        FireSlash,
        WaterWave,
        AirBlade,
        LightningBolt,
        EarthSpike,
        VoidRift,
        ExpandingMist,
        PoisonCloud,
        HealingAura,
        QiExplosion,
        DefenseBarrier,
        FormationArray
    }

    [SerializeField] private List<EffectPool> effectPools;
    private Dictionary<EffectType, Queue<GameObject>> pools;

    public static TechniqueEffectFactory Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
        InitializePools();
    }

    private void InitializePools()
    {
        pools = new Dictionary<EffectType, Queue<GameObject>>();

        foreach (var pool in effectPools)
        {
            var queue = new Queue<GameObject>();

            for (int i = 0; i < pool.initialPoolSize; i++)
            {
                var obj = Instantiate(pool.prefab);
                obj.SetActive(false);
                queue.Enqueue(obj);
            }

            pools[pool.type] = queue;
        }
    }

    /// <summary>
    /// Создаёт эффект указанного типа.
    /// </summary>
    public TechniqueEffect CreateEffect(EffectType type, Vector2 position, Vector2 direction = default)
    {
        if (!pools.ContainsKey(type))
        {
            Debug.LogError($"Effect pool not found: {type}");
            return null;
        }

        GameObject obj;

        if (pools[type].Count > 0)
        {
            obj = pools[type].Dequeue();
        }
        else
        {
            // Создаём новый если пул пуст
            var prefab = effectPools.Find(p => p.type == type)?.prefab;
            obj = Instantiate(prefab);
        }

        var effect = obj.GetComponent<TechniqueEffect>();
        effect.Play(position, direction);

        return effect;
    }

    /// <summary>
    /// Возвращает эффект в пул.
    /// </summary>
    public void ReturnEffect(EffectType type, GameObject obj)
    {
        obj.SetActive(false);
        pools[type].Enqueue(obj);
    }
}
```

---

## Использование в бою

### Пример интеграции с TechniqueController

```csharp
// Создано: 2026-04-03

public partial class TechniqueController : MonoBehaviour
{
    private void ExecuteTechniqueVisual(TechniqueData technique, Vector2 origin, Vector2 direction)
    {
        var effectType = GetEffectType(technique);
        TechniqueEffectFactory.Instance.CreateEffect(effectType, origin, direction);
    }

    private TechniqueEffectFactory.EffectType GetEffectType(TechniqueData technique)
    {
        // Определяем тип эффекта по технике
        return (technique.element, technique.type) switch
        {
            (Element.Fire, TechniqueType.Offensive) => TechniqueEffectFactory.EffectType.FireSlash,
            (Element.Water, TechniqueType.Offensive) => TechniqueEffectFactory.EffectType.WaterWave,
            (Element.Air, TechniqueType.Offensive) => TechniqueEffectFactory.EffectType.AirBlade,
            (Element.Lightning, TechniqueType.Offensive) => TechniqueEffectFactory.EffectType.LightningBolt,
            (Element.Earth, TechniqueType.Offensive) => TechniqueEffectFactory.EffectType.EarthSpike,
            (Element.Void, TechniqueType.Offensive) => TechniqueEffectFactory.EffectType.VoidRift,
            (Element.Poison, TechniqueType.Offensive) => TechniqueEffectFactory.EffectType.PoisonCloud,
            (Element.Neutral, TechniqueType.Healing) => TechniqueEffectFactory.EffectType.HealingAura,
            (Element.Neutral, TechniqueType.Defensive) => TechniqueEffectFactory.EffectType.DefenseBarrier,
            (Element.Neutral, TechniqueType.Formation) => TechniqueEffectFactory.EffectType.FormationArray,
            _ => TechniqueEffectFactory.EffectType.QiExplosion
        };
    }
}
```

---

## Структура папок спрайтов

```
UnityProject/Assets/Sprites/Combat/TechniqueEffects/
├── effect_fire_slash.png       # Огненный удар
├── effect_water_wave.png       # Водяная волна
├── effect_air_blade.png        # Воздушный клинок
├── effect_lightning_bolt.png   # Молния
├── effect_earth_spike.png      # Земляной шип
├── effect_void_rift.png        # Разрыв пустоты
├── effect_mist_expanding.png   # Расширяющийся туман
├── effect_poison_cloud.png     # Ядовитое облако
├── effect_healing_aura.png     # Аура исцеления
├── effect_qi_explosion.png     # Взрыв ци
├── effect_defense_barrier.png  # Защитный барьер
└── effect_formation_array.png  # Формационный массив
```

---

## Настройка кривых анимации

### Для расширяющихся эффектов:

```
Scale Curve:
0.0 ───────●───────────────────────● 1.0
           │                       │
         (0, 0.1)               (1, 2.0)

Alpha Curve:
0.0 ───●───────────────────────────●─── 0.0
       │                           │
     (0, 1)                      (1, 0)
```

### Для направленных эффектов:

```
Scale Curve:
0.0 ───●───────────────────────────● 1.0
       │                           │
     (0, 1)                      (1, 1.5)

Alpha Curve:
0.0 ───●───────────────●───────────● 1.0
       │               │           │
     (0, 1)         (0.7, 1)    (1, 0)
```

---

*Документ создан: 2026-04-03*
