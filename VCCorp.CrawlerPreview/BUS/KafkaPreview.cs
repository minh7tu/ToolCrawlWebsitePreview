using Confluent.Kafka;
using System;
using System.Configuration.Internal;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace VCCorp.CrawlerPreview.BUS
{
    class KafkaPreview
    {
        private const string SERVER_LINK = "10.3.48.81:9092,10.3.48.90:9092,10.3.48.91:9092";

        public static string _topicPost = "crawler-preview-post";

        public static string _topicComment = "crawler-preview-post-comment";

        private static ProducerConfig _config = new ProducerConfig
        {
            BootstrapServers = SERVER_LINK,
            ClientId = Dns.GetHostName(),
            Partitioner = Partitioner.Random
        };

        private static IProducer<string, string> producer = new ProducerBuilder<string, string>(_config)
                .SetKeySerializer(Serializers.Utf8)
                .SetValueSerializer(Serializers.Utf8)
                .Build();

        public async Task<bool> InsertPost(string messagejson, string topic)
        {
            try
            {
                DeliveryResult<string, string> val = await producer.ProduceAsync(topic, new Message<string, string> { Value = messagejson });
                producer.Flush(TimeSpan.FromMilliseconds(100));

                return true;
            }
            catch (Exception ex)
            {
                File.AppendAllText($"{Environment.CurrentDirectory}/Check/kafka.txt", ex.ToString() + "\n");
            }

            return false;
        }
        public static string ToJson<T>(T obj)
        {
            try
            {
                return System.Text.Json.JsonSerializer.Serialize<T>(obj);
            }
            catch (Exception)
            {
                return default;
            }
        }
    }

}
