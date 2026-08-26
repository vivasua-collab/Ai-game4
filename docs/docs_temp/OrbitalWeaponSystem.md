> **❌ ОТБРОШЕНО (2026-07-14):** Концепция орбитального оружия не реализована. 0 файлов в коде, 0 в Legacy. Идея оставлена для архива.

# Система орбитального оружия (→ Концепция артифактов)

**Создано:** 2026-04-03
**Версия:** 1.0
**Редактировано:** 2026-05-23 06:30:00 UTC — перенаправление концепции на систему артифактов

> ⚠️ **ПЕРЕНАПРАВЛЕНИЕ:** Концепция орбитального оружия будет использована для **системы артифактов**.
> Артифакты — это парящие вокруг персонажа магические предметы, визуально аналогичные
> орбитальному оружию, но с более широким функционалом (защита, усиление, лечение).
> Код в этом документе — legacy (без VContainer/MessagePipe). При реализации артифактов
> использовать модульную архитектуру: новый модуль `Modules/Artifact/` с VContainer LifetimeScope.

---

## Обзор

Орбитальное оружие — это оружие, которое летает вокруг персонажа по круговой орбите, указывая направление атаки. Это типичная механика для cultivation/xianxia игр.

---

## Архитектура

### Компоненты

```
Character (Center)
    │
    ├── OrbitalWeaponController
    │   │
    │   ├── orbitRadius = 1.5f       // Радиус орбиты
    │   ├── orbitSpeed = 90f         // Скорость вращения (град/сек)
    │   ├── currentAngle = 0f        // Текущий угол
    │   │
    │   └── orbitalWeapons[]         // Массив оружия на орбите
    │       ├── weapon1 (sword)
    │       ├── weapon2 (dagger)
    │       └── weapon3 (ring)
    │
    └── SpriteRenderer
        └── facingDirection (1 = right, -1 = left)
```

---

## Реализация

### OrbitalWeaponController.cs

```csharp
// Создано: 2026-04-03
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Контроллер орбитального оружия.
/// Управляет оружием, летящим по круговой орбите вокруг персонажа.
/// </summary>
public class OrbitalWeaponController : MonoBehaviour
{
    [Header("Orbit Settings")]
    [SerializeField] private float orbitRadius = 1.5f;
    [SerializeField] private float orbitSpeed = 90f; // градусов в секунду
    [SerializeField] private bool clockwiseRotation = true;

    [Header("Weapons")]
    [SerializeField] private List<OrbitalWeapon> weapons = new List<OrbitalWeapon>();

    [Header("Attack Settings")]
    [SerializeField] private float attackRotationSpeed = 360f; // Скорость при атаке
    [SerializeField] private AnimationCurve attackCurve;

    private Transform characterTransform;
    private float currentAngle;
    private bool isAttacking;

    private void Awake()
    {
        characterTransform = transform;
        currentAngle = 0f;
    }

    private void Update()
    {
        UpdateWeaponPositions();
    }

    /// <summary>
    /// Обновляет позиции оружия на орбите.
    /// </summary>
    private void UpdateWeaponPositions()
    {
        float speed = isAttacking ? attackRotationSpeed : orbitSpeed;
        int direction = clockwiseRotation ? 1 : -1;

        currentAngle += speed * direction * Time.deltaTime;
        currentAngle %= 360f;

        // Распределяем оружие равномерно по орбите
        float angleStep = 360f / weapons.Count;

        for (int i = 0; i < weapons.Count; i++)
        {
            if (weapons[i] == null || !weapons[i].IsActive) continue;

            float weaponAngle = currentAngle + (angleStep * i);
            float radians = weaponAngle * Mathf.Deg2Rad;

            Vector3 offset = new Vector3(
                Mathf.Cos(radians) * orbitRadius,
                Mathf.Sin(radians) * orbitRadius,
                0f
            );

            weapons[i].transform.position = characterTransform.position + offset;

            // Поворот оружия в направлении движения
            float rotationAngle = weaponAngle + (clockwiseRotation ? 90f : -90f);
            weapons[i].transform.rotation = Quaternion.Euler(0, 0, rotationAngle);
        }
    }

    /// <summary>
    /// Атака в указанном направлении.
    /// Оружие ускоряется и наносит урон при контакте.
    /// </summary>
    public void Attack(Vector2 direction)
    {
        if (weapons.Count == 0) return;

        StartCoroutine(AttackCoroutine(direction));
    }

    private System.Collections.IEnumerator AttackCoroutine(Vector2 direction)
    {
        isAttacking = true;

        // Вычисляем целевой угол
        float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        // Анимация атаки
        float duration = 0.3f;
        float elapsed = 0f;
        float startAngle = currentAngle;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float curveValue = attackCurve.Evaluate(t);

            // Ускоряем вращение к цели
            currentAngle = Mathf.LerpAngle(startAngle, targetAngle, curveValue);

            yield return null;
        }

        // Наносим урон
        foreach (var weapon in weapons)
        {
            if (weapon != null && weapon.IsActive)
            {
                weapon.OnAttackHit();
            }
        }

        isAttacking = false;
    }

    /// <summary>
    /// Добавляет оружие на орбиту.
    /// </summary>
    public void AddWeapon(OrbitalWeapon weapon)
    {
        if (!weapons.Contains(weapon))
        {
            weapons.Add(weapon);
            weapon.Initialize(this);
        }
    }

    /// <summary>
    /// Удаляет оружие с орбиты.
    /// </summary>
    public void RemoveWeapon(OrbitalWeapon weapon)
    {
        weapons.Remove(weapon);
    }

    /// <summary>
    /// Получает ближайшее оружие к точке.
    /// </summary>
    public OrbitalWeapon GetNearestWeapon(Vector2 point)
    {
        OrbitalWeapon nearest = null;
        float minDistance = float.MaxValue;

        foreach (var weapon in weapons)
        {
            if (weapon == null || !weapon.IsActive) continue;

            float distance = Vector2.Distance(weapon.transform.position, point);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearest = weapon;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Получает направление ближайшего оружия (для UI индикатора).
    /// </summary>
    public Vector2 GetNearestWeaponDirection()
    {
        if (weapons.Count == 0) return Vector2.right;

        float radians = currentAngle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }
}
```

### OrbitalWeapon.cs

```csharp
// Создано: 2026-04-03
using UnityEngine;

/// <summary>
/// Оружие на орбите.
/// </summary>
public class OrbitalWeapon : MonoBehaviour
{
    [Header("Weapon Settings")]
    [SerializeField] private string weaponName;
    [SerializeField] private Element element; // Огонь, Вода, и т.д.
    [SerializeField] private int baseDamage = 10;
    [SerializeField] private float hitRadius = 0.5f;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private TrailRenderer trailRenderer;
    [SerializeField] private ParticleSystem hitParticles;

    [Header("Audio")]
    [SerializeField] private AudioClip hitSound;

    private OrbitalWeaponController controller;
    private Collider2D[] hitResults = new Collider2D[10];
    private bool isActive = true;

    public bool IsActive => isActive;
    public Element Element => element;

    /// <summary>
    /// Инициализация оружия.
    /// </summary>
    public void Initialize(OrbitalWeaponController controller)
    {
        this.controller = controller;

        // Настраиваем визуальные эффекты на основе элемента
        SetupElementVisuals();
    }

    /// <summary>
    /// Настраивает визуальные эффекты в зависимости от элемента.
    /// </summary>
    private void SetupElementVisuals()
    {
        if (trailRenderer == null) return;

        // Цвет следа в зависимости от элемента
        Color trailColor = element switch
        {
            Element.Fire => new Color(1f, 0.3f, 0.1f, 0.8f),
            Element.Water => new Color(0.2f, 0.5f, 1f, 0.8f),
            Element.Lightning => new Color(0.8f, 0.8f, 1f, 0.9f),
            Element.Earth => new Color(0.6f, 0.4f, 0.2f, 0.8f),
            Element.Air => new Color(0.7f, 1f, 0.7f, 0.6f),
            Element.Poison => new Color(0.5f, 0f, 0.8f, 0.8f),
            Element.Void => new Color(0.3f, 0f, 0.5f, 0.8f),
            _ => new Color(1f, 1f, 1f, 0.5f)
        };

        trailRenderer.startColor = trailColor;
        trailRenderer.endColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0f);
    }

    /// <summary>
    /// Вызывается при попадании атаки.
    /// </summary>
    public void OnAttackHit()
    {
        // Проверяем попадания
        int hitCount = Physics2D.OverlapCircleNonAlloc(
            transform.position,
            hitRadius,
            hitResults
        );

        for (int i = 0; i < hitCount; i++)
        {
            var target = hitResults[i].GetComponent<ICombatTarget>();
            if (target != null && target.IsHostile)
            {
                // Наносим урон с учётом элемента
                DamageInfo damage = new DamageInfo
                {
                    Amount = baseDamage,
                    Element = element,
                    Source = controller.gameObject,
                    HitPoint = hitResults[i].transform.position
                };

                target.TakeDamage(damage);

                // Визуальные эффекты попадания
                OnHitEffects(hitResults[i].transform.position);
            }
        }
    }

    private void OnHitEffects(Vector2 position)
    {
        // Частицы
        if (hitParticles != null)
        {
            Instantiate(hitParticles, position, Quaternion.identity);
        }

        // Звук
        if (hitSound != null)
        {
            AudioSource.PlayClipAtPoint(hitSound, position);
        }
    }

    /// <summary>
    /// Активирует/деактивирует оружие.
    /// </summary>
    public void SetActive(bool active)
    {
        isActive = active;
        spriteRenderer.enabled = active;
        if (trailRenderer != null)
        {
            trailRenderer.enabled = active;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, hitRadius);
    }
}
```

---

## Интеграция с персонажем

### CharacterController2D.cs (дополнение)

```csharp
// Редактировано: 2026-04-03

[RequireComponent(typeof(OrbitalWeaponController))]
public class CharacterController2D : MonoBehaviour
{
    [SerializeField] private OrbitalWeaponController orbitalWeapons;

    // Направление взгляда: 1 = вправо, -1 = влево
    public int FacingDirection { get; private set; } = 1;

    private void Update()
    {
        HandleMovement();
        HandleAttack();
        UpdateFacingDirection();
    }

    private void UpdateFacingDirection()
    {
        // Определяем направление по движению или вводу
        float horizontalInput = Input.GetAxis("Horizontal");

        if (horizontalInput > 0.1f)
        {
            FacingDirection = 1;
        }
        else if (horizontalInput < -0.1f)
        {
            FacingDirection = -1;
        }

        // Зеркалим спрайт персонажа
        transform.localScale = new Vector3(FacingDirection, 1, 1);

        // Оружие на орбите продолжает вращаться независимо от направления
    }

    private void HandleAttack()
    {
        if (Input.GetMouseButtonDown(0))
        {
            // Атака в направлении курсора
            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 direction = (mousePos - (Vector2)transform.position).normalized;

            orbitalWeapons.Attack(direction);
        }
    }
}
```

---

## UI индикатор направления

### WeaponDirectionIndicator.cs

```csharp
// Создано: 2026-04-03
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI индикатор, показывающий направление ближайшего орбитального оружия.
/// </summary>
public class WeaponDirectionIndicator : MonoBehaviour
{
    [SerializeField] private OrbitalWeaponController weaponController;
    [SerializeField] private Image indicatorArrow;
    [SerializeField] private float indicatorDistance = 50f;

    private RectTransform rectTransform;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    private void Update()
    {
        if (weaponController == null) return;

        // Получаем направление ближайшего оружия
        Vector2 direction = weaponController.GetNearestWeaponDirection();

        // Поворачиваем индикатор
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        indicatorArrow.rectTransform.rotation = Quaternion.Euler(0, 0, angle);

        // Позиция индикатора относительно центра
        Vector2 position = direction * indicatorDistance;
        indicatorArrow.rectTransform.anchoredPosition = position;
    }
}
```

---

## Структура папок спрайтов

```
UnityProject/Assets/Sprites/Combat/OrbitalWeapons/
├── orbital_sword_cyan.png      # Меч с ци-аурой
├── orbital_sword_fire.png      # Огненный меч
├── orbital_dagger_purple.png   # Кинжал с фиолетовой аурой
├── orbital_axe_golden.png      # Золотой топор
├── orbital_staff_blue.png      # Посох с синим шаром
├── orbital_spear_green.png     # Копьё с зелёным следом
├── orbital_fans_wind.png       # Веера ветра
└── orbital_ring_golden.png     # Кольцевое лезвие
```

---

## Настройка в Unity

### Шаг 1: Создание префаба оружия

1. Создайте пустой GameObject `OrbitalWeapon`
2. Добавьте компоненты:
   - `SpriteRenderer` — выберите спрайт оружия
   - `TrailRenderer` — для следа при движении
   - `BoxCollider2D` — для определения попаданий
   - `OrbitalWeapon` скрипт
3. Настройте параметры элемента и урона
4. Сохраните как префаб в `Prefabs/Combat/`

### Шаг 2: Настройка персонажа

1. Добавьте компонент `OrbitalWeaponController` к персонажу
2. Укажите радиус орбиты и скорость вращения
3. Добавьте префабы оружия в список `weapons`
4. Настройте кривую анимации атаки

### Шаг 3: UI индикатор

1. Создайте Canvas с индикатором направления
2. Добавьте скрипт `WeaponDirectionIndicator`
3. Свяжите с `OrbitalWeaponController`

---

## Советы по балансу

| Параметр | Начальное значение | Для высоких уровней |
|----------|-------------------|---------------------|
| Радиус орбиты | 1.5 юнита | 2.0-3.0 юнита |
| Скорость вращения | 90°/сек | 120-180°/сек |
| Количество оружия | 1 | До 5 |
| Урон базовый | 10 | 50-100 |

---

*Документ создан: 2026-04-03*
