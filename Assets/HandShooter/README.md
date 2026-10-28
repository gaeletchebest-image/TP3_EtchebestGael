# Hand Shooter — prototipo

Abrir `Assets/Scenes/HandShooterPrototype.unity` y pulsar Play.

- WASD: movimiento. Mouse: cámara. Clic izquierdo: disparo (mantener para automático).
- Tab: abrir/cerrar configuración. Escape: abrir/cerrar y liberar el cursor.
- El menú pausa el mundo, libera el mouse y bloquea movimiento/disparos.
- Clic sobre los dedos: activar/desactivar. Solo se permite un grupo continuo de al menos un dedo. Para mover el grupo, agregar un vecino y quitar el extremo opuesto.
- Pulgar activo: raycast instantáneo con daño/espera base.
- Pulgar inactivo: proyectil visible con daño ×2 y tiempo entre disparos ×2.
- Los blancos muestran vida y último impacto; se regeneran al destruirse.

## Balance editable

Seleccionar `Assets/HandShooter/Settings/ShooterSettings.asset` en el Inspector. Cambios de balance se leen en vivo; la configuración inicial se aplica al entrar en Play. Los cambios hechos a este asset durante Play persisten, como es habitual con ScriptableObjects.

Daño = `baseDamage × additionalFingerMultiplier^(dedos activos - 1)`.

`cooldownByInactiveFingers` contiene segundos de espera para **0, 1, 2, 3 dedos inactivos**, en ese orden. Valores iniciales: `1, 0.65, 0.4, 0.2`. El Inspector exige cuatro entradas positivas y no crecientes para conservar la regla de mayor velocidad con más dedos inactivos.

| Activos | Inactivos | Raycast: daño / espera | Proyectil: daño / espera |
|---|---|---|---|
| 1 | 3 | 1 / 0.2 s | 2 / 0.4 s |
| 2 | 2 | 2 / 0.4 s | 4 / 0.8 s |
| 3 | 1 | 4 / 0.65 s | 8 / 1.3 s |
| 4 | 0 | 8 / 1 s | 16 / 2 s |

También se editan multiplicadores del pulgar, velocidad/radio/vida del proyectil, alcance, máscara de impacto, modo automático, colores, duración del rayo, movimiento, sensibilidad, FOV, teclas, vida y regeneración de blancos. La espera conserva el progreso proporcional al cambiar de configuración; abrir el menú no reinicia el arma.

## Estructura y verificación

`Runtime/`: reglas, ScriptableObject, jugador, arma, proyectil, blancos y menú uGUI con Input System.

`Editor/PrototypeSceneBuilder.cs`: menú `Hand Shooter > Open or Create Prototype`, que abre la escena existente o genera sus materiales, prefab y asset si todavía no existe. No sobrescribe una escena del prototipo ya guardada. La escena se agrega como primera escena de build, conservando SampleScene.

`Tests/EditMode`: balance de ocho combinaciones, cambios de parámetros y todos los grupos continuos.

`Tests/PlayMode`: daño raycast, espera, viaje/impacto del proyectil, barrido contra paredes finas, menú y regeneración.

La mano y los enemigos usan geometría simple de prototipo. Hay torreta, kamikaze y sniper; no se incluyen salto ni animaciones finales.

## Explosión del pulgar inactivo

El proyectil explota al impactar blancos o escenario. El blanco directo recibe el impacto más la explosión; los cercanos reciben solamente la explosión. Cada blanco recibe el área una sola vez aunque tenga varios colliders. Las paredes bloquean el daño de área. No hay caída de daño por distancia dentro del radio. Agotar alcance o vida sin impactar no produce explosión.

En `ShooterSettings.asset`, sección **Explosión del proyectil**:

- `explosionDamageFraction`: fracción del daño directo, por defecto **0.5 = 50 %**. Cero desactiva el área.
- `explosionBaseRadius`: radio base en metros, por defecto **1.25**.
- `explosionReferenceDamage`: daño directo para ese radio base, por defecto **2**.
- `explosionRadiusExponent`: crecimiento del radio, por defecto **0.5** (raíz cuadrada).
- `explosionMaxRadius`: tope de radio, por defecto **4 m**.
- `explosionVisualDuration` y `explosionColor`: duración y color de los anillos de impacto.

Radio = `min(explosionMaxRadius, explosionBaseRadius × (daño directo / explosionReferenceDamage)^explosionRadiusExponent)`.

| Dedos activos | Daño directo | Daño de explosión | Radio |
|---|---|---|---|
| 1 | 2 | 1 | 1.25 m |
| 2 | 4 | 2 | 1.77 m |
| 3 | 8 | 4 | 2.5 m |
| 4 | 16 | 8 | 3.54 m |

El HUD y el menú muestran daño de área y radio efectivos. Dos blancos pequeños flanquean al blanco central para comparar el alcance del área al concentrar más dedos. Los proyectiles conservan los valores del momento del disparo aunque después cambies la mano o el asset.

## Enemigo sniper

`Prefabs/EnemySniper.prefab` está colocado en la escena del prototipo. También se puede agregar desde `Hand Shooter > Add Sniper`. Su balance está en `Settings/SniperSettings.asset` y se lee en vivo:

- Se mueve a **2 m/s**, más lento que el kamikaze. Detecta al jugador a **26 m** y, una vez adquirido, sigue persiguiéndolo aunque se aleje o se oculte.
- Puede preparar el ataque entre **7 y 19 m** de distancia horizontal e intenta colocarse a **13 m**, el centro del rango. Retrocede si el jugador se acerca demasiado.
- Al llegar se queda inmóvil **0.4 s** para colocarse y carga durante **1.8 s**. Una línea muestra la trayectoria, un anillo se completa y el foco del cañón crece. El texto muestra el porcentaje de carga.
- Durante los últimos **0.35 s** fija la dirección: la línea se vuelve amarilla y aparece **¡FIJADO!**. El jugador puede esquivar lateralmente. Después dispara un **rayo instantáneo**, sin proyectil, y espera **1.8 s** antes de preparar otro ataque. Un impacto mata al jugador, como los otros enemigos.
- Apunta al centro del cuerpo del jugador y calcula la trayectoria desde la posición del cañón después de girar. El punto de impacto no depende de la ubicación de la cámara.
- Salir del rango o interponer cobertura cancela la carga completa. El sniper busca otra posición navegable con línea de tiro y vuelve a colocarse y cargar desde cero. Las paredes y los blancos interceptan el rayo.
- La navegación se genera una vez al entrar en Play a partir de los colliders de cada escena; no requiere horneado manual. Reutiliza ese mapa entre snipers y lo libera al descargar la escena. Si se cambia la geometría durante Play, el mapa de navegación se actualiza al volver a entrar en Play; la detección de cobertura sí es inmediata.

`Tests/PlayMode/SniperPlayTests.cs` verifica carga visible, impacto instantáneo, esquiva de la dirección fijada, espera entre disparos, ambos límites de rango, cancelación por cobertura, reposicionamiento alrededor de paredes, acercamiento/retirada, pausa y muerte.
