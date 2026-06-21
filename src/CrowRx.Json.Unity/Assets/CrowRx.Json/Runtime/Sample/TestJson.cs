using UnityEngine;

namespace CrowRx.Json.Sample
{
    public sealed class TestJson : MonoBehaviour
    {
        private void Start()
        {
            var source = new PlayerSave { Id = 7, Name = "Crow" };
            string json = JsonSerializer.Serialize(source);
            PlayerSave restored = JsonSerializer.Deserialize<PlayerSave>(json);

            Debug.Log($"CrowRx.Json round-trip: {restored.Id}, {restored.Name}");
        }

        public sealed class PlayerSave
        {
            public int Id;
            public string Name = string.Empty;
        }
    }
}
