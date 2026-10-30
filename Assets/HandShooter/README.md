# Hand Shooter — prototipo

## Defensa de enemigos

Cada enemigo tiene `initialDefensePoints` (puntos iniciales, mínimo 0) y `defenseBarCapacity` (disparos por punto, mínimo 1) en su asset de ajustes y en **Laboratorio de balance > Ajustes > enemigo > Defensa**. Solo el sniper y la torreta empiezan con **1 punto y barra de 3 impactos**. El hunter, el kamikaze y los blancos no tienen defensa y no muestran contador ni barra de defensa. Los perfiles anteriores conservan su balance sin defensa hasta configurarlos.

El daño recibido es `daño / 2^puntos`: 1 punto deja pasar el 50 %, 2 el 25 %, 3 el 12.5 %. Cada disparo del jugador que alcanza al enemigo resta **exactamente 1** a su barra, independientemente del daño, los dedos o el tipo de disparo. Un proyectil con impacto directo y explosión cuenta una sola vez por enemigo, incluso con varios colliders; los enemigos alcanzados únicamente por el área también pierden una unidad. Los disparos fallados o bloqueados por cobertura no la gastan.

El impacto que vacía la barra usa la defensa que había al recibirlo; después se pierde un punto y la barra se rellena para el siguiente. Al llegar a 0 puntos desaparecen el contador y la barra azul que acompañan a la vida. La defensa reduce también el daño de otros enemigos, pero solo los disparos del jugador desgastan la barra. Reiniciar el enemigo o repetir el encuentro restablece su defensa inicial. Los blancos de práctica siguen sin defensa.

El indicador de impacto y el historial registran el daño después de la reducción. En **Comparar**, el daño y DPS corresponden a la defensa inicial; impactos, tiempo y combustible para matar incluyen cómo se pierde con cada disparo.

## Laboratorio de balance

Abrir **Hand Shooter > Laboratorio de balance**. La ventana tiene cuatro pestañas:

1. **Ajustes:** editar mano, explosión, combustible, movimiento y cada enemigo. Los valores se guardan en `Assets/HandShooter/Balance/Borrador.asset`, una copia independiente de los ajustes del juego. Se crea también el perfil **Base inicial** para poder recuperar los valores anteriores.
2. **Comparar:** elegir enemigo, distancia y cantidad de blancos juntos. La tabla muestra las ocho variantes de estadísticas de la mano: daño directo y de área, radio, cadencia, DPS, DPS con recargas, impactos para matar, tiempo ideal, combustible por baja y disparos por tanque. Arrastrar un perfil a Referencia permite comparar versiones. La vida personalizada permite comparar armas contra la misma vida, aunque los perfiles cambien la vida del enemigo.
3. **Pruebas e historial:** elegir blanco estático, hunter, sniper, kamikazes, torreta o grupo mixto; ajustar distancia, cantidad, separación, cobertura, tiempo y mano inicial; pulsar **Probar borrador**. Se abre la escena independiente `Assets/Scenes/BalanceLab.unity`, que reutiliza el jugador, HUD y prefabs reales. Se genera al preparar la primera prueba. Al detener Play desde la ventana vuelve a abrirse la escena anterior. Unity ofrece guardar las escenas modificadas antes de entrar a la prueba.
4. **Perfiles:** nombrar el borrador, anotar la hipótesis, guardar versiones, cargar una versión y consultar sus diferencias. **Aplicar borrador al juego** copia los ajustes a los cinco assets originales y el combustible al arma de la escena del prototipo. Si esa escena ya estaba abierta, guardar la escena para conservar el combustible; los assets se guardan al aplicar. Para revertir, cargar y aplicar **Base inicial**. Los perfiles guardados no cambian al seguir editando el borrador.

Flujo recomendado: guardar una versión, cambiar una o dos variables, revisar la tabla, probar el mismo encuentro varias veces y comparar resultados. No se busca igualar todos los DPS: considerar combustible, distancia, explosión, precisión y margen de reacción.

Durante las pruebas: WASD y mouse, clic para disparar, R para recargar, Tab para pausar/configurar la mano y **F5 para repetir**. El panel se cierra al iniciar para dejar visible el juego. Al terminar, **Volver al laboratorio** detiene Play, restaura la escena anterior y abre el historial; detener Play manualmente tiene el mismo efecto. Para editar durante una prueba, volver a abrir la ventana desde el menú. Repetir recarga la escena: reinicia enemigos, posiciones, mano, tanques y espera del arma. Los ajustes del borrador se leen en vivo sobre copias de ejecución; la vida inicial y el combustible inicial se aplican al repetir. Una prueba con cambios en vivo queda marcada y se excluye de la comparación automática A/B.

Cada prueba registra tiempo de juego (excluye pausas), disparos, porcentaje de disparos que dañaron algún blanco, bajas atribuibles al arma, combustible gastado, recargas completadas, daño efectivo limitado por la vida restante y causa de muerte. Una explosión que alcanza varios enemigos cuenta como un único disparo acertado. Las muertes por explosiones de otros enemigos no se atribuyen al arma. La prueba termina al despejar el encuentro, morir, agotar el tiempo o finalizarla manualmente; salir antes registra una interrupción.

El historial conserva las últimas 100 pruebas en `UserSettings/HandShooterBalanceHistory.json` (local, fuera de Git). La ventana muestra las últimas 20 y compara con la prueba anterior que tenga igual configuración de encuentro y resultado. **Exportar CSV** y **Exportar JSON con ajustes** incluyen el historial; JSON conserva también snapshots completos de los ajustes iniciales/finales. Esto permite conocer los parámetros de una prueba aunque el perfil se haya modificado después.

Los tiempos de la tabla son estimaciones ideales: primer disparo disponible, impactos perfectos, objetivo quieto, sin cobertura y recarga inmediata cuando hace falta. La espera del arma transcurre durante la recarga. El daño directo del proyectil incluye la explosión cuando tiene radio; el daño a grupos supone que todos están dentro del área y sin cobertura. El DPS sostenido supone suministro de tanques; el tiempo para matar sí respeta las reservas iniciales. **No viable** significa que falta combustible o que el objetivo excede el alcance/la duración del proyectil. Los valores reales se contrastan jugando.

Abrir `Assets/Scenes/HandShooterPrototype.unity` y pulsar Play.

- WASD: movimiento. Mouse: cámara. Clic izquierdo: disparo (mantener para automático).
- Tab: abrir/cerrar configuración. Escape: abrir/cerrar y liberar el cursor.
- El menú pausa el mundo, libera el mouse y bloquea movimiento/disparos.
- Clic sobre los dedos: activar/desactivar. Solo se permite un grupo continuo de al menos un dedo. Para mover el grupo, agregar un vecino y quitar el extremo opuesto.
- Con el menú abierto, clic en un espacio o tecla **1–9 / 0**: guardar o reemplazar la configuración actual (dedos exactos y pulgar). Cada espacio muestra los dedos guardados y el tipo de disparo; los que coinciden con la mano actual se resaltan.
- Durante la partida, **1–9 / 0** activa la configuración guardada. También funciona el teclado numérico. Los espacios vacíos no hacen nada. Se conservan entre partidas y al cerrar el juego mediante PlayerPrefs; cambiar de configuración mantiene el combustible, la recarga y el progreso de espera entre disparos.
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
