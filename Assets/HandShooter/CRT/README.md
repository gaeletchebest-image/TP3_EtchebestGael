# CRT moderado

Implementación para **Unity 6000.3.11f1 / URP 17.3 / Render Graph**. No instala paquetes.

## Uso

Ya está conectado al `Player.prefab` usado en Tutorial y LevelStarter, y a las cámaras de HandShooterPrototype y BalanceLab. SampleScene queda como escena de ejemplo sin CRT.

1. Abrí una escena del juego y pulsá Play.
2. Seleccioná `Main Camera` del jugador. En **CRT Camera Effect** aparece el perfil y sus parámetros desplegados.
3. También podés seleccionar directamente `Assets/HandShooter/CRT/ModerateCRT.asset`.
4. Para desactivar todo, desmarcá el componente **CRT Camera Effect**, o **Effect Enabled** en el perfil. **Intensity = 0** también omite el efecto completo y la reducción de resolución.

El perfil es compartido: los cambios afectan todas las escenas que lo usan. Para ajustes por cámara, duplicá el perfil y asigná la copia. Como otros ScriptableObjects, las modificaciones del perfil durante Play pueden persistir al salir.

## Ajustes iniciales

| Efecto | Interruptor / parámetros | Valor inicial |
| --- | --- | --- |
| Resolución real | Reduced Resolution / Resolution Scale | Activada / 0.67 por eje |
| Escalado | Upscaling | Bilinear; alternativa Point |
| Scanlines | Scanlines / Intensity / Count / Thinness | 0.16 / 240 / 3 |
| Curvatura | Curvature / Amount | 0.025 |
| Esquinas | Rounded Corners / Corner Radius | 0.012 del lado menor |
| Ruido | Analog Noise / Amount / Frames Per Second / Grain Size | 0.009 / 24 / 1 |
| Aberración RGB | Chromatic Aberration / Offset Pixels | 0.4 píxeles de pantalla |
| Viñeta | Vignette / Intensity / Start / Softness | 0.12 / 0.4 / 0.6 |

Cada interruptor funciona de forma independiente. Desactivar **Reduced Resolution** usa escala 1 mientras mantiene los efectos visuales. **Intensity** regula los efectos visuales; **Resolution Scale** controla la resolución por separado.

Para comparar Bilinear y Point, dejá **Reduced Resolution** activa y cambiá **Upscaling**. Es el escalador nativo de URP; no agrega un desenfoque. El muestreo de la curvatura usa interpolación lineal para evitar escalones en la deformación.

## Renderizado e interfaz

- `CRTCameraEffect` aplica temporalmente el Render Scale y el filtro de URP antes de inicializar la cámara base. Ambos se conservan hasta terminar la última cámara Overlay y se restauran al terminar la pila. La resolución reducida se usa al dibujar la geometría, profundidad y transparencias, no sólo sobre una copia final. A 1920 × 1080, escala 0.67 produce 1286 × 723 (redondeo entero de URP).
- `CRTRendererFeature` está instalado en `PC_Renderer.asset` y `Mobile_Renderer.asset`. Procesa una sola vez la imagen completa de mundo + mano, después del postprocesamiento y antes del escalado/presentación de URP. Usa un pase de Render Graph y sustituye el color activo; no copia de vuelta innecesariamente ni pide texturas de profundidad/normales adicionales.
- La escala por eje mantiene el aspecto con el redondeo de un píxel propio de URP. No se fija una resolución 4:3 ni se estira la cámara. Las texturas se redimensionan con el viewport.
- La curvatura no hace zoom para esconder bordes: deja un marco negro estrecho. Las esquinas redondeadas ocultan sólo una zona mínima configurable. Para eliminar ese pequeño enmascarado, desactivá **Rounded Corners**.
- Las scanlines no se desplazan y se limitan a una línea por cada tres píxeles internos para evitar aliasing. El ruido cambia con tiempo no escalado y sólo varía por píxel, sin interferencias ni cambios globales de brillo.
- Los Canvas **Screen Space - Overlay** existentes, incluidos HUD y menús, se dibujan después del CRT a resolución completa. Para excluir UI nueva elegí ese mismo Render Mode. Los Canvas World Space y Screen Space - Camera forman parte de la escena procesada.
- Se omiten Scene View, previews, reflejos, cámaras con Target Texture y XR. Las miniaturas de guardado con destino RenderTexture conservan su ruta existente. Esta implementación apunta al juego de escritorio actual, con Render Graph habilitado; no usa OnRenderImage ni el modo Compatibility.
- Conservá activa la feature del renderer y usá el componente/perfil para apagar el sistema completo. Desactivar sólo la feature del renderer omite el shader, pero el componente sigue controlando la resolución.

## Cámaras nuevas

En una cámara base nueva: agregá **Rendering > CRT Camera Effect** y asigná `ModerateCRT.asset`. Usá uno de los renderers configurados. No agregues el componente a la cámara Overlay de la mano: hereda la configuración de la base y recibe el CRT al terminar la pila.

Para volver a instalar las features y configurar cámaras MainCamera de escenas abiertas: **Tools > Hand Shooter > CRT > Configure loaded cameras and renderers**, y guardá la escena. El comando es idempotente. Para renderers personalizados, agregá **CRT Renderer Feature** y asigná `Shaders/CRT.shader` al campo **Crt Shader**. La referencia explícita mantiene el shader incluido en los builds.

## Archivos

- `Runtime/CRTProfile.cs`: parámetros e interruptores.
- `Runtime/CRTCameraEffect.cs`: resolución real y filtro por pila de cámaras; restaura URP al desactivarse.
- `Runtime/CRTRendererFeature.cs`: postproceso sobre el color final del mundo y la mano.
- `Shaders/CRT.shader`: scanlines, curvatura, máscara de esquinas, ruido, RGB y viñeta.
- `Editor/CRTCameraEffectEditor.cs`: edición del perfil dentro del Inspector de la cámara y diagnóstico del último buffer.
- `Editor/CRTSetup.cs`: instalación y configuración reutilizable.
- `ModerateCRT.asset`: valores iniciales compartidos.
- `Tests/Editor/CRTShaderTests.cs`: pruebas GPU de bypass, curvatura/aspecto, scanlines, ruido y viñeta.

Las carpetas usan assemblies propios para no cambiar las dependencias de los sistemas de gameplay.

## Verificación

Las pruebas GPU se ejecutan en **Window > General > Test Runner > EditMode**, assembly `HandShooter.CRT.Tests`. Incluyen 4:3, 16:9, 21:9 y orientación vertical, y verifican que desactivar los efectos conserve el brillo de toda la imagen. La resolución real y la pila mundo/mano también se verifican en Play Mode desde el diagnóstico **Último buffer CRT**.

Referencia de la API utilizada: [Unity 6.3: Blit using the render graph system](https://docs.unity.com/en-us/engine/6000.3/manual/render-pipelines/universal-render-pipeline/customizing-urp/render-graph/blit).
