using System;
using System.Linq;
using System.Net;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using qDNS.Model;

namespace qDNS.Tests
{
	[TestClass]
	public class MessageSerializationTests
	{


		[TestMethod]
		public void Should_parse_request()
		{
			var requestString = "0001010000010000000000000131013001300331323707696E2D61646472046172706100000C0001";
			// 0001 0100 0001 0000 0000 0000
			// 01 31 01 30 01 30 03 31 32 37 07 69 6E 2D 61 64 64 72 04 61 72 70 61 00
			// 000C 0001
			var requestData = StringToByteArray(requestString);

			var req = Request.Parse(requestData);

			var str = JsonConvert.SerializeObject(req, Formatting.Indented);

			Console.WriteLine(str);

			Assert.AreEqual(1, req.Header.Identifiation);
			Assert.AreEqual(1, req.Questions.Count);
			Assert.AreEqual(0, req.Answers.Count);
			Assert.AreEqual(0, req.AuthorityRR.Count);
			Assert.AreEqual(0, req.AdditionalRR.Count);
		}

		[TestMethod]
		public void Should_parse_request2()
		{
			var requestString =
				"0002010000010000000000000367686504736F7469036E657404636F727004736F7469036E65740000010001";
			// 0002 0100 0001 0000 0000 0000
			// 0367686504736F7469036E657404636F727004736F7469036E657400
			// 0001 0001
			// sotinetcorpsotinet
			var requestData = StringToByteArray(requestString);

			const int skip = 12;
			var hex = string.Join(" ", requestData.Skip(skip).Select(x => x.ToString("X2")));
			var ascii = string.Join(" ",
				Encoding.GetEncoding(1251).GetString(requestData).Skip(skip).Select(x => " " + x));
			Console.WriteLine(hex);
			Console.WriteLine(ascii);


			var req = Request.Parse(requestData);

			var str = JsonConvert.SerializeObject(req, Formatting.Indented);

			Console.WriteLine(str);

			Assert.AreEqual(2, req.Header.Identifiation);
			Assert.AreEqual(1, req.Questions.Count);
			Assert.AreEqual(0, req.Answers.Count);
			Assert.AreEqual(0, req.AuthorityRR.Count);
			Assert.AreEqual(0, req.AdditionalRR.Count);
			Assert.AreEqual(1, req.Questions.Count);
			Assert.AreEqual("ghe.soti.net.corp.soti.net", req.Questions[0].Name);
			Assert.AreEqual(RecordType.A, req.Questions[0].Type);
			Assert.AreEqual(RecordClass.IN, req.Questions[0].Class);
		}



		[TestMethod]
		public void Should_parse_request3()
		{
			var requestString = "00020100000100000000000006676F6F676C6503636F6D0000010001";

			// 0002 0100 0001 0000 0000 0000
			// 0367686504736F7469036E657404636F727004736F7469036E657400
			// 0001 0001
			// sotinetcorpsotinet
			var requestData = StringToByteArray(requestString);

			const int skip = 12;
			var hex = string.Join(" ", requestData.Skip(skip).Select(x => x.ToString("X2")));
			var ascii = string.Join(" ",
				Encoding.GetEncoding(1251).GetString(requestData).Skip(skip).Select(x => " " + x));
			Console.WriteLine(hex);
			Console.WriteLine(ascii);


			var req = Request.Parse(requestData);

			var str = JsonConvert.SerializeObject(req, Formatting.Indented);

			Console.WriteLine(str);

			Assert.AreEqual(2, req.Header.Identifiation);
			// Assert.AreEqual(HeaderFlags.Query, req.Header.Flags);
			Assert.AreEqual(1, req.Questions.Count);
			Assert.AreEqual(0, req.Answers.Count);
			Assert.AreEqual(0, req.AuthorityRR.Count);
			Assert.AreEqual(0, req.AdditionalRR.Count);
			Assert.AreEqual(1, req.Questions.Count);
			Assert.AreEqual("google.com", req.Questions[0].Name);
			Assert.AreEqual(RecordType.A, req.Questions[0].Type);
			Assert.AreEqual(RecordClass.IN, req.Questions[0].Class);
		}

		// 00020100000100000000000006676F6F676C6503636F6D0000010001

		public static byte[] StringToByteArray(string hex)
		{
			return Enumerable.Range(0, hex.Length)
				.Where(x => x % 2 == 0)
				.Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
				.ToArray();
		}


		[TestMethod]
		public void Should_parse_response()
		{
			var responseHex = "0006818000010001000000000279610272750000010001C00C00010001000000A4000457FAFAF2";

			// 00070100000100000000000002796102727500001C0001
			// 0006818000010001000000000279610272750000010001C00C00010001000000A4000457FAFAF2

			// 0006 8180 0001 0001 0000 0000 02 7961 02 7275 00 0001 0001 C00C 0001 0001 000000A4 0004 57FAFAF2
			//   id   FL    Q    A               y a     r u    TYPE CLAS      TYPE CLAS      TTL  LEN       IP

			var responseData = StringToByteArray(responseHex);

			const int skip = 0;
			var hex = string.Join(" ", responseData.Skip(skip).Select(x => x.ToString("X2")));
			var ascii = string.Join(" ",
				Encoding.GetEncoding(1251).GetString(responseData).Skip(skip).Select(x => " " + x));
			Console.WriteLine(hex);
			Console.WriteLine(ascii);

			var res = Request.Parse(responseData);

			var str = JsonConvert.SerializeObject(res, Formatting.Indented);

			Console.WriteLine(str);

			Assert.AreEqual(6, res.Header.Identifiation);
			// Assert.AreEqual(HeaderFlags.Query, res.Header.Flags);
			Assert.AreEqual(1, res.Questions.Count);
			Assert.AreEqual(1, res.Answers.Count);
			Assert.AreEqual(0, res.AuthorityRR.Count);
			Assert.AreEqual(0, res.AdditionalRR.Count);
			Assert.AreEqual(1, res.Questions.Count);
			Assert.AreEqual("ya.ru", res.Questions[0].Name);


			Assert.AreEqual(1, res.Answers.Count);
			Assert.AreEqual("ya.ru", res.Answers[0].Name);
			Assert.AreEqual(RecordType.A, res.Answers[0].Type);
			Assert.AreEqual(RecordClass.IN, res.Answers[0].Class);
			Assert.AreEqual(164, res.Answers[0].Ttl);
			Assert.AreEqual(4, res.Answers[0].Data.Length);
			Console.WriteLine("IP:" + string.Join(".", res.Answers[0].Data.Select(x => x.ToString())));
			CollectionAssert.AreEqual(new byte[] {87, 250, 250, 242}, res.Answers[0].Data);

			var ser = res.Serialize();
			Console.WriteLine(string.Join(" ", responseData.Select(x => x.ToString("X2"))));
			Console.WriteLine(string.Join(" ", ser.Select(x => x.ToString("X2"))));

			Assert.AreEqual(responseData.Length, ser.Length);

			for (int i = 0; i < responseData.Length; i++)
			{
				Assert.AreEqual(responseData[i], ser[i]);
			}
		}

		[TestMethod]
		public void Should_serialize_response()
		{
			var requestString = "00020100000100000000000006676F6F676C6503636F6D0000010001";
			var requestData = StringToByteArray(requestString);
			var req = Request.Parse(requestData);

			req.Header.Flags = (HeaderFlags) 0x8180;
			req.Answers.Add(new ResponseRecord
			{
				Ttl = 60 * 3,
				Type = RecordType.A,
				Class = RecordClass.IN,
				Name = req.Questions[0].Name,
				Data = new byte[] {10, 0, 0, 22},
			});

			var ser = req.Serialize();
			Console.WriteLine(string.Join(" ", ser.Select(x => x.ToString("X2"))));

			var dser = Response.Parse(ser);

			Assert.AreEqual(1, dser.Answers.Count);
			Assert.AreEqual("google.com", dser.Answers[0].Name);
			Assert.AreEqual(RecordType.A, dser.Answers[0].Type);
			Assert.AreEqual(RecordClass.IN, dser.Answers[0].Class);
			Assert.AreEqual(60 * 3, dser.Answers[0].Ttl);

			CollectionAssert.AreEqual(new byte[] {10, 0, 0, 22}, dser.Answers[0].Data);
		}
	}
}
