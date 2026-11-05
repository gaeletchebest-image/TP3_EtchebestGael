#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HandShooter.Tests
{
    public sealed class LevelStarterLiftPlayTests
    {
        [UnityTest]
        public IEnumerator PlayerArrivesAndBoardsTheDepartureLiftWithoutInput()
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("LevelStarter");
            var player = Object.FindFirstObjectByType<PrototypePlayer>();
            player.SetMenuOpen(false);
            player.enabled = false;
            var rider = player.GetComponent<CharacterController>();
            var arrival = GameObject.Find("Arrival Lift").transform;
            var arrivalCar = arrival.Find("Carriage");
            var arrivalGate = arrivalCar.Find("Gate");
            Assert.That(arrivalCar.localPosition.y, Is.GreaterThan(3.5f));
            player.transform.position = arrivalCar.position + Vector3.up * 0.08f;
            Physics.SyncTransforms();
            Assert.That(arrivalGate.localPosition.y, Is.Zero.Within(0.01f));
            yield return WaitFor(() => arrivalGate.localPosition.y > 2f);
            Assert.That(arrivalCar.localPosition.y, Is.Zero.Within(0.01f));
            Assert.That(player.transform.position.y - arrivalCar.position.y, Is.InRange(0f, 0.25f));
            for (int step = 0; step < 23; step++)
                rider.Move(new Vector3(0f, -0.035f, 0.12f));
            Assert.That(player.transform.position.z, Is.GreaterThan(arrivalCar.position.z + 1.7f));

            var departure = GameObject.Find("Departure Lift").transform;
            var departureCar = departure.Find("Carriage");
            var departureGate = departureCar.Find("Gate");
            float upperY = departureCar.position.y;
            float height = new SerializedObject(departure.GetComponent<CargoLift>()).FindProperty("travelHeight").floatValue;
            Assert.That(upperY, Is.EqualTo(departure.position.y).Within(0.01f));
            Assert.That(departureGate.localPosition.y, Is.GreaterThan(2f));

            // Walk the actual approach and dock from just past the last arena's exit.
            player.transform.position = new Vector3(departure.position.x, upperY + 0.08f, 164.5f);
            Physics.SyncTransforms();
            for (int step = 0; step < 45; step++)
                rider.Move(new Vector3(0f, -0.05f, 0.15f));
            Assert.That(player.transform.position.z, Is.EqualTo(171.25f).Within(0.2f));
            yield return new WaitForSeconds(0.25f);
            Assert.That(departureGate.localPosition.y, Is.GreaterThan(2f));

            for (int step = 0; step < 8; step++)
                rider.Move(new Vector3(0f, -0.05f, 0.15f));
            Assert.That(player.transform.position.z, Is.GreaterThan(departure.position.z - 0.5f));
            yield return WaitFor(() => departureCar.position.y < upperY - 0.01f);
            Assert.That(departureGate.localPosition.y, Is.Zero.Within(0.01f));
            yield return WaitFor(() => departureCar.localPosition.y <= -height + 0.01f);
            Assert.That(player.transform.position.y - departureCar.position.y, Is.InRange(0f, 0.25f));
            Assert.That(departureGate.localPosition.y, Is.Zero.Within(0.01f));
        }

        private static IEnumerator WaitFor(System.Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 8f;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, "The automatic lift did not reach the expected pose.");
        }

        [UnityTest]
        public IEnumerator ArrivalRaisesAnAuthoredRiderAndDescendsAtConfiguredSpeed()
        {
            Time.timeScale = 1f;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/HandShooter/Prefabs/CargoLift/CargoLift.prefab");
            var staging = new GameObject("Test arrival");
            staging.transform.position = new Vector3(900f, 35f, 900f);
            staging.SetActive(false);
            var rider = new GameObject("Authored rider").AddComponent<CharacterController>();
            rider.height = 1.8f;
            rider.center = Vector3.up * 0.9f;
            rider.transform.position = staging.transform.position + Vector3.up * 0.08f;

            try
            {
                var lift = Object.Instantiate(prefab, staging.transform).GetComponent<CargoLift>();
                var settings = new SerializedObject(lift);
                settings.FindProperty("operation").enumValueIndex = 1;
                settings.FindProperty("travelHeight").floatValue = 6f;
                settings.FindProperty("travelSpeed").floatValue = 3f;
                settings.ApplyModifiedPropertiesWithoutUndo();

                var carriage = lift.transform.Find("Carriage");
                staging.SetActive(true);
                Assert.That(carriage.localPosition.y, Is.EqualTo(6f).Within(0.001f));
                Assert.That(rider.transform.position.y - staging.transform.position.y, Is.EqualTo(6.08f).Within(0.001f));

                yield return WaitFor(() => carriage.localPosition.y < 5.8f);
                float sampleHeight = carriage.localPosition.y;
                float sampleTime = Time.time;
                yield return WaitFor(() => Time.time - sampleTime >= 0.2f);
                Assert.That(sampleHeight - carriage.localPosition.y,
                    Is.EqualTo((Time.time - sampleTime) * 3f).Within(0.01f));

                yield return WaitFor(() => carriage.localPosition.y < 0.001f);
                Assert.That(carriage.position.y, Is.EqualTo(lift.transform.position.y).Within(0.001f));
                Assert.That(rider.transform.position.y - staging.transform.position.y, Is.EqualTo(0.08f).Within(0.03f));
            }
            finally
            {
                Object.DestroyImmediate(rider.gameObject);
                Object.DestroyImmediate(staging);
            }
        }
    }

    public sealed class CargoLiftPlayTests
    {
        private const string PrefabPath = "Assets/HandShooter/Prefabs/CargoLift/CargoLift.prefab";
        private CargoLift lift;
        private CharacterController rider;
        private Transform carriage;
        private Transform gate;

        [SetUp]
        public void SetUp()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<CargoLift>();
            lift = Object.Instantiate(prefab, new Vector3(1000f, 20f, 1000f), Quaternion.identity);
            carriage = lift.transform.Find("Carriage");
            gate = carriage.Find("Gate");
            rider = new GameObject("Test lift rider").AddComponent<CharacterController>();
            rider.center = Vector3.up * 0.9f;
            rider.height = 1.8f;
            rider.radius = 0.3f;
            rider.minMoveDistance = 0f;
        }

        [TearDown]
        public void TearDown()
        {
            if (rider) Object.DestroyImmediate(rider.gameObject);
            if (lift) Object.DestroyImmediate(lift.gameObject);
        }

        [UnityTest]
        public IEnumerator ClosesBeforeMovingAndCarriesPlayerToBothStops()
        {
            PlaceRider(Vector3.up * 0.2f);
            rider.Move(Vector3.down * 0.3f);
            float standingClearance = rider.transform.position.y - carriage.position.y;
            float guideDepth = gate.localPosition.z;
            lift.OpenGate();
            yield return Until(() => gate.localPosition.y > 2f);
            Assert.That(gate.localPosition.z, Is.EqualTo(guideDepth).Within(0.001f));

            lift.GoUp();
            yield return Until(() => carriage.localPosition.y > 0f);
            Assert.That(gate.localPosition.y, Is.Zero.Within(0.001f));
            Assert.That(gate.localPosition.z, Is.EqualTo(guideDepth).Within(0.001f));
            yield return Until(() => gate.localPosition.y > 2f);
            Assert.That(carriage.localPosition.y, Is.EqualTo(4f).Within(0.001f));
            Assert.That(rider.transform.position.y - carriage.position.y, Is.EqualTo(standingClearance).Within(0.03f));

            lift.GoDown();
            yield return Until(() => carriage.localPosition.y < 4f);
            yield return Until(() => gate.localPosition.y > 2f);
            Assert.That(carriage.localPosition.y, Is.Zero.Within(0.001f));
            Assert.That(rider.transform.position.y - carriage.position.y, Is.EqualTo(standingClearance).Within(0.03f));
        }

        [Test]
        public void ShotsCanPassBetweenTheBars()
        {
            PlaceRider(Vector3.zero);
            Assert.That(HitsLift(new Vector3(0f, 0.8f, -2.2f), Vector3.forward, 1.1f), Is.True);
            Assert.That(HitsLift(new Vector3(0.1f, 0.8f, -2.2f), Vector3.forward, 1.1f), Is.False);
            Assert.That(HitsLift(new Vector3(0.4f, 0.85f, 0.85f), Vector3.right, 1.2f), Is.False);
        }

        [UnityTest]
        public IEnumerator ReopensIfAPlayerStepsIntoTheEntranceDuringClosing()
        {
            PlaceRider(Vector3.zero);
            lift.OpenGate();
            yield return Until(() => gate.localPosition.y > 2f);
            lift.GoUp();
            yield return Until(() => gate.localPosition.y < 1.85f);

            PlaceRider(new Vector3(0f, 0.02f, -1.49f));
            yield return Until(() => gate.localPosition.y > 2f);
            Assert.That(carriage.localPosition.y, Is.Zero.Within(0.001f));
            PlaceRider(Vector3.zero);
            yield return new WaitForSeconds(0.9f);
            Assert.That(carriage.localPosition.y, Is.Zero.Within(0.001f));
            Assert.That(gate.localPosition.y, Is.GreaterThan(2f));
        }

        private void PlaceRider(Vector3 localPosition)
        {
            rider.transform.position = carriage.TransformPoint(localPosition);
            Physics.SyncTransforms();
        }

        private bool HitsLift(Vector3 from, Vector3 direction, float distance) =>
            Physics.Raycast(carriage.TransformPoint(from), direction, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        private static IEnumerator Until(System.Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 8f;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, "The lift did not reach the expected pose.");
        }
    }
}
#endif
