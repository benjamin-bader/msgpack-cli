﻿
 
#region -- License Terms --
//
// MessagePack for CLI
//
// Copyright (C) 2010-2015 FUJIWARA, Yusuke
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
//
#endregion -- License Terms --

#pragma warning disable 3003
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
#if !NETFX_35 && !WINDOWS_PHONE
using System.Numerics;
#endif // !NETFX_35 && !WINDOWS_PHONE
using System.Reflection;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Serialization;
using System.Text;
#if !NETFX_CORE && !WINDOWS_PHONE && !UNITY_IPHONE && !UNITY_ANDROID && !XAMIOS && !XAMDROID
using MsgPack.Serialization.CodeDomSerializers;
using MsgPack.Serialization.EmittingSerializers;
#endif // !NETFX_CORE && !WINDOWS_PHONE && !UNITY_IPHONE && !UNITY_ANDROID && !XAMIOS && !XAMDROID
#if !NETFX_35 && !UNITY_IPHONE && !UNITY_ANDROID && !XAMIOS && !XAMDROID
using MsgPack.Serialization.ExpressionSerializers;
#endif // !NETFX_35 && !UNITY_IPHONE && !UNITY_ANDROID && !XAMIOS && !XAMDROID
#if !MSTEST
using NUnit.Framework;
#else
using TestFixtureAttribute = Microsoft.VisualStudio.TestPlatform.UnitTestFramework.TestClassAttribute;
using TestAttribute = Microsoft.VisualStudio.TestPlatform.UnitTestFramework.TestMethodAttribute;
using TimeoutAttribute = NUnit.Framework.TimeoutAttribute;
using Assert = NUnit.Framework.Assert;
using Is = NUnit.Framework.Is;
#endif

namespace MsgPack.Serialization
{
	[TestFixture]
	[Timeout( 30000 )]
	public class ArrayReflectionBasedReflectionMessagePackSerializerTest
	{
		private SerializationContext GetSerializationContext()
		{
			return new SerializationContext { SerializationMethod = SerializationMethod.Array, EmitterFlavor = EmitterFlavor.ReflectionBased };
		}

		private SerializationContext  NewSerializationContext( PackerCompatibilityOptions compatibilityOptions )
		{
			return new SerializationContext( compatibilityOptions ) { SerializationMethod = SerializationMethod.Array, EmitterFlavor = EmitterFlavor.ReflectionBased };
		}

		private MessagePackSerializer<T> CreateTarget<T>( SerializationContext context )
		{
			return context.GetSerializer<T>( context );
		}
		
		private bool CanDump
		{
			get { return false; }
		}

#if !NETFX_CORE && !WINDOWS_PHONE && !XAMIOS && !XAMDROID && !UNITY_IPHONE && !UNITY_ANDROID
		[SetUp]
		public void SetUp()
		{
			SerializerDebugging.DeletePastTemporaries();
			//SerializerDebugging.TraceEnabled = true;
			//SerializerDebugging.DumpEnabled = true;
			if ( SerializerDebugging.TraceEnabled )
			{
				Tracer.Emit.Listeners.Clear();
				Tracer.Emit.Switch.Level = SourceLevels.All;
				Tracer.Emit.Listeners.Add( new ConsoleTraceListener() );
			}

			SerializerDebugging.OnTheFlyCodeDomEnabled = true;
			SerializerDebugging.AddRuntimeAssembly( typeof( AddOnlyCollection<> ).Assembly.Location );
			if( typeof( AddOnlyCollection<> ).Assembly != this.GetType().Assembly )
			{
				SerializerDebugging.AddRuntimeAssembly( this.GetType().Assembly.Location );
			}
		}

		[TearDown]
		public void TearDown()
		{
			if ( SerializerDebugging.DumpEnabled && this.CanDump )
			{
				try
				{
					SerializerDebugging.Dump();
				}
				catch ( NotSupportedException ex )
				{
					Console.Error.WriteLine( ex );
				}
				finally
				{
					DefaultSerializationMethodGeneratorManager.Refresh();
				}
			}

			SerializerDebugging.Reset();
			SerializerDebugging.OnTheFlyCodeDomEnabled = false;
		}
#endif // !NETFX_CORE && !WINDOWS_PHONE && !XAMIOS && !XAMDROID && !UNITY_IPHONE && !UNITY_ANDROID

		private void DoKnownCollectionTest<T>( SerializationContext context )
			where T : new()
		{
			using ( var buffer = new MemoryStream() )
			{
				CreateTarget<T>( context ).Pack( buffer, new T() );
			}
		}

		[Test]
		public void TestUnpackTo()
		{
			var target = this.CreateTarget<Int32[]>( GetSerializationContext() );
			using ( var buffer = new MemoryStream() )
			{
				target.Pack( buffer, new[] { 1, 2 } );
				buffer.Position = 0;
				int[] result = new int[ 2 ];
				using ( var unpacker = Unpacker.Create( buffer, false ) )
				{
					unpacker.Read();
					target.UnpackTo( unpacker, result );
					Assert.That( result, Is.EqualTo( new[] { 1, 2 } ) );
				}
			}
		}

		[Test]
		public void TestInt32()
		{
			TestCore( 1, stream => Unpacking.UnpackInt32( stream ), null );
		}

		[Test]
		public void TestInt64()
		{
			TestCore( Int32.MaxValue + 1L, stream => Unpacking.UnpackInt64( stream ), null );
		}

		[Test]
		public void TestString()
		{
			TestCore( "abc", stream => Unpacking.UnpackString( stream ), null );
		}

		[Test]
		public void TestDateTime()
		{
			TestCore(
				DateTime.UtcNow,
				stream => MessagePackConvert.ToDateTime( Unpacking.UnpackInt64( stream ) ),
				CompareDateTime
			);
		}

		[Test]
		public void TestDateTimeOffset()
		{
			TestCore(
				DateTimeOffset.UtcNow,
				stream => MessagePackConvert.ToDateTimeOffset( Unpacking.UnpackInt64( stream ) ),
				( x, y ) => CompareDateTime( x.DateTime.ToUniversalTime(), y.DateTime.ToUniversalTime() )
			);
		}

		private static bool CompareDateTime( DateTime x, DateTime y )
		{
			return x.Date == y.Date && x.Hour == y.Hour && x.Minute == y.Minute && x.Second == y.Second && x.Millisecond == y.Millisecond;
		}

		[Test]
		public void TestUri()
		{
			TestCore( new Uri( "http://www.example.com" ), stream => new Uri( Unpacking.UnpackString( stream ) ), null );
		}

		[Test]
		public void TestComplexObject_WithShortcut()
		{
			SerializerDebugging.AvoidsGenericSerializer = false;
			try 
			{
				this.TestComplexObjectCore( this.GetSerializationContext() );
			}
			finally
			{
				SerializerDebugging.AvoidsGenericSerializer = false;
			}
		}

		[Test]
		public void TestComplexObject_WithoutShortcut()
		{
			SerializerDebugging.AvoidsGenericSerializer = true;
			try 
			{
				this.TestComplexObjectCore( this.GetSerializationContext() );
			}
			finally
			{
				SerializerDebugging.AvoidsGenericSerializer = false;
			}
		}

		private void TestComplexObjectCore( SerializationContext context )
		{
			var target = new ComplexType() { Source = new Uri( "http://www.exambple.com" ), TimeStamp = DateTime.Now, Data = new byte[] { 0x1, 0x2, 0x3, 0x4 } };
			target.History.Add( DateTime.Now.Subtract( TimeSpan.FromDays( 1 ) ), "Create New" );
			target.Points.Add( 123 );
			TestCoreWithVerify( target, context );
		}

		[Test]
		public void TestComplexTypeWithoutAnyAttribute_WithShortcut()
		{
			SerializerDebugging.AvoidsGenericSerializer = false;
			try 
			{
				this.TestComplexTypeWithoutAnyAttribute( this.GetSerializationContext() );
			}
			finally
			{
				SerializerDebugging.AvoidsGenericSerializer = false;
			}		}

		[Test]
		public void TestComplexTypeWithoutAnyAttribute_WithoutShortcut()
		{
			SerializerDebugging.AvoidsGenericSerializer = true;
			try 
			{
				this.TestComplexTypeWithoutAnyAttribute( this.GetSerializationContext() );
			}
			finally
			{
				SerializerDebugging.AvoidsGenericSerializer = false;
			}		}

		private void TestComplexTypeWithoutAnyAttribute( SerializationContext context )
		{
			var target = new ComplexTypeWithoutAnyAttribute() { Source = new Uri( "http://www.exambple.com" ), TimeStamp = DateTime.Now, Data = new byte[] { 0x1, 0x2, 0x3, 0x4 } };
			target.History.Add( DateTime.Now.Subtract( TimeSpan.FromDays( 1 ) ), "Create New" );
			TestCoreWithVerify( target, context );
		}

		[Test]
		public void TestTypeWithMissingMessagePackMemberAttributeMember()
		{
			this.TestTypeWithMissingMessagePackMemberAttributeMemberCore( this.GetSerializationContext() );
		}

		private void TestTypeWithMissingMessagePackMemberAttributeMemberCore( SerializationContext context )
		{
			var target = new TypeWithMissingMessagePackMemberAttributeMember();
			TestCoreWithVerify( target, context );
		}

		[Test]
		public void TestTypeWithInvalidMessagePackMemberAttributeMember()
		{
			Assert.Throws<SerializationException>( () => this.CreateTarget<TypeWithInvalidMessagePackMemberAttributeMember>( GetSerializationContext() ) );
		}

		[Test]
		public void TestTypeWithDuplicatedMessagePackMemberAttributeMember()
		{
			Assert.Throws<SerializationException>( () => this.CreateTarget<TypeWithDuplicatedMessagePackMemberAttributeMember>( GetSerializationContext() ) );
		}

		[Test]
		public void TestComplexObjectTypeWithDataContract_WithShortcut()
		{
			SerializerDebugging.AvoidsGenericSerializer = false;
			try 
			{
				this.TestComplexObjectTypeWithDataContractCore( this.GetSerializationContext() );
			}
			finally
			{
				SerializerDebugging.AvoidsGenericSerializer = false;
			}
		}

		[Test]
		public void TestComplexObjectTypeWithDataContract_WithoutShortcut()
		{
			SerializerDebugging.AvoidsGenericSerializer = true;
			try 
			{
				this.TestComplexObjectTypeWithDataContractCore( this.GetSerializationContext() );
			}
			finally
			{
				SerializerDebugging.AvoidsGenericSerializer = false;
			}
		}

		private void TestComplexObjectTypeWithDataContractCore( SerializationContext context )
		{
			var target = new ComplexTypeWithDataContract() { Source = new Uri( "http://www.exambple.com" ), TimeStamp = DateTime.Now, Data = new byte[] { 0x1, 0x2, 0x3, 0x4 } };
			target.History.Add( DateTime.Now.Subtract( TimeSpan.FromDays( 1 ) ), "Create New" );
#if !NETFX_CORE && !SILVERLIGHT
			target.NonSerialized = new DefaultTraceListener();
#else
			target.NonSerialized = new Stopwatch();
#endif // !NETFX_CORE && !SILVERLIGHT
			TestCoreWithVerify( target, context );
		}

		[Test]
		public void TestComplexTypeWithDataContractWithOrder_WithShortcut()
		{
			SerializerDebugging.AvoidsGenericSerializer = false;
			try 
			{
				this.TestComplexTypeWithDataContractWithOrderCore( this.GetSerializationContext() );
			}
			finally
			{
				SerializerDebugging.AvoidsGenericSerializer = false;
			}
		}

		[Test]
		public void TestComplexTypeWithDataContractWithOrder_WithoutShortcut()
		{
			SerializerDebugging.AvoidsGenericSerializer = true;
			try 
			{
				this.TestComplexTypeWithDataContractWithOrderCore( this.GetSerializationContext() );
			}
			finally
			{
				SerializerDebugging.AvoidsGenericSerializer = false;
			}
		}

		private void TestComplexTypeWithDataContractWithOrderCore( SerializationContext context )
		{
			var target = new ComplexTypeWithDataContractWithOrder() { Source = new Uri( "http://www.exambple.com" ), TimeStamp = DateTime.Now, Data = new byte[] { 0x1, 0x2, 0x3, 0x4 } };
			target.History.Add( DateTime.Now.Subtract( TimeSpan.FromDays( 1 ) ), "Create New" );
#if !NETFX_CORE && !SILVERLIGHT
			target.NonSerialized = new DefaultTraceListener();
#else
			target.NonSerialized = new Stopwatch();
#endif
			TestCoreWithVerify( target, context );
		}

		[Test]
		public void TestComplexObjectTypeWithNonSerialized_WithShortcut()
		{
			SerializerDebugging.AvoidsGenericSerializer = false;
			try 
			{
				this.TestComplexObjectTypeWithNonSerializedCore( this.GetSerializationContext() );
			}
			finally
			{
				SerializerDebugging.AvoidsGenericSerializer = false;
			}
		}

		[Test]
		public void TestComplexObjectTypeWithNonSerialized_WithoutShortcut()
		{
			SerializerDebugging.AvoidsGenericSerializer = true;
			try 
			{
				this.TestComplexObjectTypeWithNonSerializedCore( this.GetSerializationContext() );
			}
			finally
			{
				SerializerDebugging.AvoidsGenericSerializer = false;
			}
	}

		private void TestComplexObjectTypeWithNonSerializedCore( SerializationContext context )
		{
			var target = new ComplexTypeWithNonSerialized() { Source = new Uri( "http://www.exambple.com" ), TimeStamp = DateTime.Now, Data = new byte[] { 0x1, 0x2, 0x3, 0x4 } };
			target.History.Add( DateTime.Now.Subtract( TimeSpan.FromDays( 1 ) ), "Create New" );
#if !NETFX_CORE && !SILVERLIGHT
			target.NonSerialized = new DefaultTraceListener();
#endif
			TestCoreWithVerify( target, context );
		}

		[Test]
		public void TestDataMemberAttributeOrderWithOneBase()
		{
			var context = GetSerializationContext();
			var value = new ComplexTypeWithOneBaseOrder();
			var target = this.CreateTarget<ComplexTypeWithOneBaseOrder>( context );
			using ( var buffer = new MemoryStream() )
			{
				target.Pack( buffer, value );
				buffer.Position = 0;
				var unpacked = target.Unpack( buffer );
				Assert.That( unpacked.One, Is.EqualTo( value.One ) );
				Assert.That( unpacked.Two, Is.EqualTo( value.Two ) );
			}
		}

		[Test]
		public void TestDataMemberAttributeOrderWithOneBase_ProtoBufCompatible()
		{
			var context = GetSerializationContext();
			context.CompatibilityOptions.OneBoundDataMemberOrder = true;
			var value = new ComplexTypeWithOneBaseOrder();
			var target = this.CreateTarget<ComplexTypeWithOneBaseOrder>( context );
			using ( var buffer = new MemoryStream() )
			{
				target.Pack( buffer, value );
				buffer.Position = 0;
				var unpacked = target.Unpack( buffer );
				Assert.That( unpacked.One, Is.EqualTo( value.One ) );
				Assert.That( unpacked.Two, Is.EqualTo( value.Two ) );
			}
		}

		[Test]
		public void TestDataMemberAttributeOrderWithOneBaseDeserialize()
		{
			var context = GetSerializationContext();
			context.SerializationMethod = SerializationMethod.Array;
			var target = this.CreateTarget<ComplexTypeWithOneBaseOrder>( context );
			using ( var buffer = new MemoryStream() )
			{
				buffer.Write( new byte[] { 0x93, 0xff, 10, 20 } );
				buffer.Position = 0;
				var unpacked = target.Unpack( buffer );
				Assert.That( unpacked.One, Is.EqualTo( 10 ) );
				Assert.That( unpacked.Two, Is.EqualTo( 20 ) );
			}
		}

		[Test]
		public void TestDataMemberAttributeOrderWithOneBaseDeserialize_ProtoBufCompatible()
		{
			var context = GetSerializationContext();
			context.SerializationMethod = SerializationMethod.Array;
			context.CompatibilityOptions.OneBoundDataMemberOrder = true;
			var target = this.CreateTarget<ComplexTypeWithOneBaseOrder>( context );
			using ( var buffer = new MemoryStream() )
			{
				buffer.Write( new byte[] { 0x92, 10, 20 } );
				buffer.Position = 0;
				var unpacked = target.Unpack( buffer );
				Assert.That( unpacked.One, Is.EqualTo( 10 ) );
				Assert.That( unpacked.Two, Is.EqualTo( 20 ) );
			}
		}

		[Test]
		public void TestDataMemberAttributeOrderWithZeroBase_ProtoBufCompatible_Fail()
		{
			var context = GetSerializationContext();
			context.SerializationMethod = SerializationMethod.Array;
			context.CompatibilityOptions.OneBoundDataMemberOrder = true;
			Assert.Throws<NotSupportedException>(
				() => this.CreateTarget<ComplexTypeWithTwoMember>( context )
			);
		}

		[Test]
		public void TestDataMemberAttributeNamedProperties()
		{
			var context = GetSerializationContext();
			if ( context.SerializationMethod == SerializationMethod.Array )
			{
				// Nothing to test.
				return;
			}

			var value = new DataMemberAttributeNamedPropertyTestTarget() { Member = "A Member" };
			var target = this.CreateTarget<DataMemberAttributeNamedPropertyTestTarget>( context );
			using ( var buffer = new MemoryStream() )
			{
				target.Pack( buffer, value );
				buffer.Position = 0;
				var asDictionary = Unpacking.UnpackDictionary( buffer );

				Assert.That( asDictionary[ "Alias" ] == value.Member );

				buffer.Position = 0;

				var unpacked = target.Unpack( buffer );
				Assert.That( unpacked.Member, Is.EqualTo( value.Member ) );
			}
		}

		[Test]
		public void TestEnum()
		{
			TestCore( DayOfWeek.Sunday, stream => ( DayOfWeek )Enum.Parse( typeof( DayOfWeek ), Unpacking.UnpackString( stream ) ), ( x, y ) => x == y );
		}

#if !NETFX_CORE && !SILVERLIGHT
		[Test]
		public void TestNameValueCollection()
		{
			var target = new NameValueCollection();
			target.Add( String.Empty, "Empty-1" );
			target.Add( String.Empty, "Empty-2" );
			target.Add( "1", "1-1" );
			target.Add( "1", "1-2" );
			target.Add( "1", "1-3" );
			target.Add( "null", null ); // This value will not be packed.
			target.Add( "Empty", String.Empty );
			target.Add( "2", "2" );
			var serializer = this.CreateTarget<NameValueCollection>( this.GetSerializationContext() );
			using ( var stream = new MemoryStream() )
			{
				serializer.Pack( stream, target );
				stream.Position = 0;
				NameValueCollection result = serializer.Unpack( stream );
				Assert.That( result.GetValues( String.Empty ), Is.EquivalentTo( new[] { "Empty-1", "Empty-2" } ) );
				Assert.That( result.GetValues( "1" ), Is.EquivalentTo( new[] { "1-1", "1-2", "1-3" } ) );
				Assert.That( result.GetValues( "null" ), Is.Null );
				Assert.That( result.GetValues( "Empty" ), Is.EquivalentTo( new string[] { String.Empty } ) );
				Assert.That( result.GetValues( "2" ), Is.EquivalentTo( new string[] { "2" } ) );
				// null only value is not packed.
				Assert.That( result.Count, Is.EqualTo( target.Count - 1 ) );
			}
		}

		[Test]
		public void TestNameValueCollection_NullKey()
		{
			var target = new NameValueCollection();
			target.Add( null, "null" );
			var serializer = this.CreateTarget<NameValueCollection>( this.GetSerializationContext() );
			using ( var stream = new MemoryStream() )
			{
				Assert.Throws<NotSupportedException>( () => serializer.Pack( stream, target ) );
			}
		}
#endif

		[Test]
		public void TestByteArrayContent()
		{
			var serializer = this.CreateTarget<byte[]>( this.GetSerializationContext() );
			using ( var stream = new MemoryStream() )
			{
				serializer.Pack( stream, new byte[] { 1, 2, 3, 4 } );
				stream.Position = 0;
				Assert.That( Unpacking.UnpackBinary( stream ).ToArray(), Is.EqualTo( new byte[] { 1, 2, 3, 4 } ) );
			}
		}

		[Test]
		public void TestCharArrayContent()
		{
			var serializer = this.CreateTarget<char[]>( GetSerializationContext() );
			using ( var stream = new MemoryStream() )
			{
				serializer.Pack( stream, new char[] { 'a', 'b', 'c', 'd' } );
				stream.Position = 0;
				Assert.That( Unpacking.UnpackString( stream ), Is.EqualTo( "abcd" ) );
			}
		}

#if !NETFX_35
		[Test]
		public void TestTuple1()
		{
			TestTupleCore( new Tuple<int>( 1 ) );
		}

		[Test]
		public void TestTuple7()
		{
			TestTupleCore( new Tuple<int, string, int, string, int, string, int>( 1, "2", 3, "4", 5, "6", 7 ) );
		}

		[Test]
		public void TestTuple8()
		{
			TestTupleCore(
				new Tuple<
				int, string, int, string, int, string, int,
				Tuple<string>>(
					1, "2", 3, "4", 5, "6", 7,
					new Tuple<string>( "8" )
				)
			);
		}

		[Test]
		public void TestTuple14()
		{
			TestTupleCore(
				new Tuple<
				int, string, int, string, int, string, int,
				Tuple<
				string, int, string, int, string, int, string
				>
				>(
					1, "2", 3, "4", 5, "6", 7,
					new Tuple<string, int, string, int, string, int, string>(
						"8", 9, "10", 11, "12", 13, "14"
					)
				)
			);
		}

		[Test]
		public void TestTuple15()
		{
			TestTupleCore(
				new Tuple<
				int, string, int, string, int, string, int,
				Tuple<
				string, int, string, int, string, int, string,
				Tuple<int>
				>
				>(
					1, "2", 3, "4", 5, "6", 7,
					new Tuple<string, int, string, int, string, int, string, Tuple<int>>(
						"8", 9, "10", 11, "12", 13, "14",
						new Tuple<int>( 15 )
					)
				)
			);
		}

		private void TestTupleCore<T>( T expected )
			where T : IStructuralEquatable
		{
			var serializer = this.CreateTarget<T>( GetSerializationContext() );
			using ( var stream = new MemoryStream() )
			{
				serializer.Pack( stream, expected );
				stream.Position = 0;
				Assert.That( serializer.Unpack( stream ), Is.EqualTo( expected ) );
			}
		}
#endif // !NETFX_35

		[Test]
		public void TestEmptyBytes()
		{
			var serializer = this.CreateTarget<byte[]>( GetSerializationContext() );
			using ( var stream = new MemoryStream() )
			{
				serializer.Pack( stream, new byte[ 0 ] );
				Assert.That( stream.Length, Is.EqualTo( 1 ), BitConverter.ToString( stream.ToArray() ) );
				stream.Position = 0;
				Assert.That( serializer.Unpack( stream ), Is.EqualTo( new byte[ 0 ] ) );
			}
		}

		[Test]
		public void TestEmptyString()
		{
			var serializer = this.CreateTarget<string>( GetSerializationContext() );
			using ( var stream = new MemoryStream() )
			{
				serializer.Pack( stream, String.Empty );
				Assert.That( stream.Length, Is.EqualTo( 1 ) );
				stream.Position = 0;
				Assert.That( serializer.Unpack( stream ), Is.EqualTo( String.Empty ) );
			}
		}

		[Test]
		public void TestEmptyIntArray()
		{
			var serializer = this.CreateTarget<int[]>( GetSerializationContext() );
			using ( var stream = new MemoryStream() )
			{
				serializer.Pack( stream, new int[ 0 ] );
				Assert.That( stream.Length, Is.EqualTo( 1 ) );
				stream.Position = 0;
				Assert.That( serializer.Unpack( stream ), Is.EqualTo( new int[ 0 ] ) );
			}
		}

		[Test]
		public void TestEmptyKeyValuePairArray()
		{
			var serializer = this.CreateTarget<KeyValuePair<string, int>[]>( GetSerializationContext() );
			using ( var stream = new MemoryStream() )
			{
				serializer.Pack( stream, new KeyValuePair<string, int>[ 0 ] );
				Assert.That( stream.Length, Is.EqualTo( 1 ) );
				stream.Position = 0;
				Assert.That( serializer.Unpack( stream ), Is.EqualTo( new KeyValuePair<string, int>[ 0 ] ) );
			}
		}

		[Test]
		public void TestEmptyMap()
		{
			var serializer = this.CreateTarget<Dictionary<string, int>>( GetSerializationContext() );
			using ( var stream = new MemoryStream() )
			{
				serializer.Pack( stream, new Dictionary<string, int>() );
				Assert.That( stream.Length, Is.EqualTo( 1 ) );
				stream.Position = 0;
				Assert.That( serializer.Unpack( stream ), Is.EqualTo( new Dictionary<string, int>() ) );
			}
		}

		[Test]
		public void TestNullable()
		{
			var serializer = this.CreateTarget<int?>( GetSerializationContext() );
			using ( var stream = new MemoryStream() )
			{
				serializer.Pack( stream, 1 );
				Assert.That( stream.Length, Is.EqualTo( 1 ) );
				stream.Position = 0;
				Assert.That( serializer.Unpack( stream ), Is.EqualTo( 1 ) );

				stream.Position = 0;
				serializer.Pack( stream, null );
				Assert.That( stream.Length, Is.EqualTo( 1 ) );
				stream.Position = 0;
				Assert.That( serializer.Unpack( stream ), Is.EqualTo( null ) );
			}
		}

		[Test]
		public void TestValueType_Success()
		{
			var serializer = this.CreateTarget<TestValueType>( GetSerializationContext() );
			using ( var stream = new MemoryStream() )
			{
				var value = new TestValueType() { StringField = "ABC", Int32ArrayField = new int[] { 1, 2, 3 }, DictionaryField = new Dictionary<int, int>() { { 1, 1 } } };
				serializer.Pack( stream, value );
				stream.Position = 0;
				var result = serializer.Unpack( stream );
				Assert.That( result.StringField, Is.EqualTo( value.StringField ) );
				Assert.That( result.Int32ArrayField, Is.EqualTo( value.Int32ArrayField ) );
				Assert.That( result.DictionaryField, Is.EqualTo( value.DictionaryField ) );
			}
		}

		[Test]
		public void TestHasInitOnlyField_Fail()
		{
			Assert.Throws<SerializationException>( () => this.CreateTarget<HasInitOnlyField>( GetSerializationContext() ) );
		}

		[Test]
		public void TestHasGetOnlyProperty_Fail()
		{
			Assert.Throws<SerializationException>( () => this.CreateTarget<HasGetOnlyProperty>( GetSerializationContext() ) );
		}

		[Test]
		public void TestCollection_Success()
		{
			var serializer = this.CreateTarget<Collection<int>>( GetSerializationContext() );
			using ( var stream = new MemoryStream() )
			{
				var value = new Collection<int>() { 1, 2, 3 };
				serializer.Pack( stream, value );
				stream.Position = 0;
				var result = serializer.Unpack( stream );
				Assert.That( result.ToArray(), Is.EqualTo( new int[] { 1, 2, 3 } ) );
			}
		}

		[Test]
		public void TestIListValueType_Success()
		{
			var serializer = this.CreateTarget<ListValueType<int>>( GetSerializationContext() );
			using ( var stream = new MemoryStream() )
			{
				var value = new ListValueType<int>( 3 ) { 1, 2, 3 };
				serializer.Pack( stream, value );
				stream.Position = 0;
				var result = serializer.Unpack( stream );
				Assert.That( result.ToArray(), Is.EqualTo( new int[] { 1, 2, 3 } ) );
			}
		}

		[Test]
		public void TestIDictionaryValueType_Success()
		{
			var serializer = this.CreateTarget<DictionaryValueType<int, int>>( GetSerializationContext() );
			using ( var stream = new MemoryStream() )
			{
				var value = new DictionaryValueType<int, int>( 3 ) { { 1, 1 }, { 2, 2 }, { 3, 3 } };
				serializer.Pack( stream, value );
				stream.Position = 0;
				var result = serializer.Unpack( stream );
				Assert.That( result.ToArray(), Is.EquivalentTo( Enumerable.Range( 1, 3 ).Select( i => new KeyValuePair<int, int>( i, i ) ).ToArray() ) );
			}
		}

		[Test]
		public void TestPackable_PackToMessageUsed()
		{
			var serializer = this.CreateTarget<JustPackable>( GetSerializationContext() );
			using ( var stream = new MemoryStream() )
			{
				var value = new JustPackable();
				value.Int32Field = 1;
				serializer.Pack( stream, value );
				Assert.That( stream.ToArray(), Is.EqualTo( new byte[] { 0x91, 0xA1, ( byte )'A' } ) );
				stream.Position = 0;
				Assert.Throws<SerializationException>( () => serializer.Unpack( stream ), "Round-trip should not be succeeded." );
			}
		}

		[Test]
		public void TestUnpackable_UnpackFromMessageUsed()
		{
			var serializer = this.CreateTarget<JustUnpackable>( GetSerializationContext() );
			using ( var stream = new MemoryStream() )
			{
				var value = new JustUnpackable();
				serializer.Pack( stream, value );
				stream.Position = 0;
				var result = serializer.Unpack( stream );
				Assert.That( result.Int32Field.ToString(), Is.EqualTo( JustUnpackable.Dummy ) );
			}
		}

		[Test]
		public void TestPackableUnpackable_PackToMessageAndUnpackFromMessageUsed()
		{
			var serializer = this.CreateTarget<PackableUnpackable>( GetSerializationContext() );
			using ( var stream = new MemoryStream() )
			{
				var value = new PackableUnpackable();
				value.Int32Field = 1;
				serializer.Pack( stream, value );
				Assert.That( stream.ToArray(), Is.EqualTo( new byte[] { 0x91, 0xA1, ( byte )'A' } ) );
				stream.Position = 0;
				serializer.Unpack( stream );
			}
		}

		[Test]
		public void TestBinary_DefaultContext()
		{
			var serializer = SerializationContext.Default.GetSerializer<byte[]>();

			using ( var stream = new MemoryStream() )
			{
				serializer.Pack( stream, new byte[] { 1 } );
				Assert.That( stream.ToArray(), Is.EqualTo( new byte[] { MessagePackCode.MinimumFixedRaw + 1, 1 } ) );
			}
		}

		[Test]
		public void TestBinary_ContextWithPackerCompatilibyOptionsNone()
		{
			var context = NewSerializationContext( PackerCompatibilityOptions.None );
			var serializer = CreateTarget<byte[]>( context );

			using ( var stream = new MemoryStream() )
			{
				serializer.Pack( stream, new byte[] { 1 } );
				Assert.That( stream.ToArray(), Is.EqualTo( new byte[] { MessagePackCode.Bin8, 1, 1 } ) );
			}
		}
		[Test]
		public void TestExt_DefaultContext()
		{
			var context = NewSerializationContext( SerializationContext.Default.CompatibilityOptions.PackerCompatibilityOptions );
			context.Serializers.Register( new CustomDateTimeSerealizer() );
			var serializer = CreateTarget<DateTime>( context );

			using ( var stream = new MemoryStream() )
			{
				var date = DateTime.UtcNow;
				serializer.Pack( stream, date );
				stream.Position = 0;
				var unpacked = serializer.Unpack( stream );
				Assert.That( unpacked.ToString( "yyyyMMddHHmmssfff" ), Is.EqualTo( date.ToString( "yyyyMMddHHmmssfff" ) ) );
			}
		}

		[Test]
		public void TestExt_ContextWithPackerCompatilibyOptionsNone()
		{
			var context = NewSerializationContext( PackerCompatibilityOptions.None );
			context.Serializers.Register( new CustomDateTimeSerealizer() );
			context.CompatibilityOptions.PackerCompatibilityOptions = PackerCompatibilityOptions.None;
			var serializer = CreateTarget<DateTime>( context );

			using ( var stream = new MemoryStream() )
			{
				var date = DateTime.UtcNow;
				serializer.Pack( stream, date );
				stream.Position = 0;
				var unpacked = serializer.Unpack( stream );
				Assert.That( unpacked.ToString( "yyyyMMddHHmmssfff" ), Is.EqualTo( date.ToString( "yyyyMMddHHmmssfff" ) ) );
			}
		}

		[Test]
		public void TestAbstractTypes_KnownCollections_Default_Success()
		{
			var context = NewSerializationContext( PackerCompatibilityOptions.None );
			var serializer = CreateTarget<WithAbstractInt32Collection>( context );

			using ( var stream = new MemoryStream() )
			{
				var value = new WithAbstractInt32Collection() { Collection = new[] { 1, 2 } };
				serializer.Pack( stream, value );
				stream.Position = 0;
				var unpacked = serializer.Unpack( stream );
				Assert.That( unpacked.Collection, Is.Not.Null.And.InstanceOf<List<int>>() );
				Assert.That( unpacked.Collection[ 0 ], Is.EqualTo( 1 ) );
				Assert.That( unpacked.Collection[ 1 ], Is.EqualTo( 2 ) );
			}
		}

		[Test]
		public void TestAbstractTypes_KnownCollections_WithoutRegistration_Fail()
		{
			var context = NewSerializationContext( PackerCompatibilityOptions.None );
			context.DefaultCollectionTypes.Unregister( typeof( IList<> ) );
			Assert.Throws<NotSupportedException>( () => DoKnownCollectionTest<WithAbstractInt32Collection>( context ) );
		}

		[Test]
		public void TestAbstractTypes_KnownCollections_ExplicitRegistration_Success()
		{
			var context = NewSerializationContext( PackerCompatibilityOptions.None );
			context.DefaultCollectionTypes.Register( typeof( IList<> ), typeof( Collection<> ) );
			var serializer = CreateTarget<WithAbstractInt32Collection>( context );

			using ( var stream = new MemoryStream() )
			{
				var value = new WithAbstractInt32Collection() { Collection = new[] { 1, 2 } };
				serializer.Pack( stream, value );
				stream.Position = 0;
				var unpacked = serializer.Unpack( stream );
				Assert.That( unpacked.Collection, Is.Not.Null.And.InstanceOf<Collection<int>>() );
				Assert.That( unpacked.Collection[ 0 ], Is.EqualTo( 1 ) );
				Assert.That( unpacked.Collection[ 1 ], Is.EqualTo( 2 ) );
			}
		}

		[Test]
		public void TestAbstractTypes_KnownCollections_ExplicitRegistrationForSpecific_Success()
		{
			var context = NewSerializationContext( PackerCompatibilityOptions.None );
			context.DefaultCollectionTypes.Register( typeof( IList<int> ), typeof( Collection<int> ) );
			var serializer1 = CreateTarget<WithAbstractInt32Collection>( context );

			using ( var stream = new MemoryStream() )
			{
				var value = new WithAbstractInt32Collection() { Collection = new[] { 1, 2 } };
				serializer1.Pack( stream, value );
				stream.Position = 0;
				var unpacked = serializer1.Unpack( stream );
				Assert.That( unpacked.Collection, Is.Not.Null.And.InstanceOf<Collection<int>>() );
				Assert.That( unpacked.Collection[ 0 ], Is.EqualTo( 1 ) );
				Assert.That( unpacked.Collection[ 1 ], Is.EqualTo( 2 ) );
			}

			// check other types are not affected
			var serializer2 = CreateTarget<WithAbstractStringCollection>( context );

			using ( var stream = new MemoryStream() )
			{
				var value = new WithAbstractStringCollection() { Collection = new[] { "1", "2" } };
				serializer2.Pack( stream, value );
				stream.Position = 0;
				var unpacked = serializer2.Unpack( stream );
				Assert.That( unpacked.Collection, Is.Not.Null.And.InstanceOf<List<string>>() );
				Assert.That( unpacked.Collection[ 0 ], Is.EqualTo( "1" ) );
				Assert.That( unpacked.Collection[ 1 ], Is.EqualTo( "2" ) );
			}
		}

		[Test]
		public void TestAbstractTypes_NotACollection_Fail()
		{
			var context = NewSerializationContext( PackerCompatibilityOptions.None );
			Assert.Throws<NotSupportedException>( () => DoKnownCollectionTest<WithAbstractNonCollection>( context ) );
		}

		// FIXME: init-only field, get-only property, Value type which implements IList<T> and has .ctor(int), Enumerator class which explicitly implements IEnumerator

		private void TestCore<T>( T value, Func<Stream, T> unpacking, Func<T, T, bool> comparer )
		{
			var safeComparer = comparer ?? EqualityComparer<T>.Default.Equals;
			var target = this.CreateTarget<T>( GetSerializationContext() );
			using ( var buffer = new MemoryStream() )
			{
				target.Pack( buffer, value );
				buffer.Position = 0;
				T intermediate = unpacking( buffer );
				Assert.That( safeComparer( intermediate, value ), "Expected:{1}{0}Actual :{2}", Environment.NewLine, value, intermediate );
				buffer.Position = 0;
				T unpacked = target.Unpack( buffer );
				Assert.That( safeComparer( unpacked, value ), "Expected:{1}{0}Actual :{2}", Environment.NewLine, value, unpacked );
			}
		}

		private void TestCoreWithVerify<T>( T value, SerializationContext context )
			where T : IVerifiable
		{
			var target = this.CreateTarget<T>( context );
			using ( var buffer = new MemoryStream() )
			{
				target.Pack( buffer, value );
				buffer.Position = 0;
				T unpacked = target.Unpack( buffer );
				buffer.Position = 0;
				unpacked.Verify( buffer );
			}
		}

		[Test]
		public void TestIssue25_Plain()
		{
			var hasEnumerable = new HasEnumerable { Numbers = new[] { 1, 2 } };
			var target = CreateTarget<HasEnumerable>( this.GetSerializationContext() );

			using ( var buffer = new MemoryStream() )
			{
				target.Pack( buffer, hasEnumerable );
				buffer.Position = 0;
				var result = target.Unpack( buffer );
				var resultNumbers = result.Numbers.ToArray();
				Assert.That( resultNumbers.Length, Is.EqualTo( 2 ) );
				Assert.That( resultNumbers[ 0 ], Is.EqualTo( 1 ) );
				Assert.That( resultNumbers[ 1 ], Is.EqualTo( 2 ) );
			}
		}

		[Test]
		public void TestIssue25_SelfComposite()
		{
			SerializationContext serializationContext =
				SerializationContext.Default;
			try
			{

				serializationContext.Serializers.Register( new PersonSerializer() );
				serializationContext.Serializers.Register( new ChildrenSerializer() );

				object[] array = new object[] { new Person { Name = "Joe" }, 3 };

				MessagePackSerializer<object[]> context =
					serializationContext.GetSerializer<object[]>();

				byte[] packed = context.PackSingleObject( array ); 
				object[] unpacked = context.UnpackSingleObject( packed );

				Assert.That( unpacked.Length, Is.EqualTo( 2 ) );
				Assert.That( ( ( MessagePackObject )unpacked[ 0 ] ).AsDictionary()[ "Name" ].AsString(), Is.EqualTo( "Joe" ) );
				Assert.That( ( ( MessagePackObject )unpacked[ 0 ] ).AsDictionary()[ "Children" ].IsNil );
				Assert.That( ( MessagePackObject )unpacked[ 1 ], Is.EqualTo( new MessagePackObject( 3 ) ) );
			}
			finally
			{
				SerializationContext.Default = new SerializationContext();
			}
		}

#region -- ReadOnly / Private Members --

		// ReSharper disable UnusedMember.Local
		// member names
		private const string PublicProperty = "PublicProperty";
		private const string PublicReadOnlyProperty = "PublicReadOnlyProperty";
		private const string NonPublicProperty = "NonPublicProperty";
		private const string PublicPropertyPlain = "PublicPropertyPlain";
		private const string PublicReadOnlyPropertyPlain = "PublicReadOnlyPropertyPlain";
		private const string NonPublicPropertyPlain = "NonPublicPropertyPlain";
		private const string CollectionReadOnlyProperty = "CollectionReadOnlyProperty";
		private const string PublicField = "PublicField";
		private const string PublicReadOnlyField = "PublicReadOnlyField";
		private const string NonPublicField = "NonPublicField";
		private const string PublicFieldPlain = "PublicFieldPlain";
		private const string PublicReadOnlyFieldPlain = "PublicReadOnlyFieldPlain";
		private const string NonPublicFieldPlain = "NonPublicFieldPlain";
#if !NETFX_CORE && !SILVERLIGHT
		private const string NonSerializedPublicField = "NonSerializedPublicField";
		private const string NonSerializedPublicReadOnlyField = "NonSerializedPublicReadOnlyField";
		private const string NonSerializedNonPublicField = "NonSerializedNonPublicField";
		private const string NonSerializedPublicFieldPlain = "NonSerializedPublicFieldPlain";
		private const string NonSerializedPublicReadOnlyFieldPlain = "NonSerializedPublicReadOnlyFieldPlain";
		private const string NonSerializedNonPublicFieldPlain = "NonSerializedNonPublicFieldPlain";
#endif // !NETFX_CORE && !SILVERLIGHT
		// ReSharper restore UnusedMember.Local

		[Test]
		public void TestNonPublicWritableMember_PlainOldCliClass()
		{
			var target = new PlainClass();
			target.CollectionReadOnlyProperty.Add( 10 );
			TestNonPublicWritableMemberCore( target, PublicProperty, PublicField, CollectionReadOnlyProperty );
		}

		[Test]
		public void TestNonPublicWritableMember_MessagePackMember()
		{
			var target = new AnnotatedClass();
			target.CollectionReadOnlyProperty.Add( 10 );
#if !NETFX_CORE && !SILVERLIGHT
			TestNonPublicWritableMemberCore( target, PublicProperty, NonPublicProperty, PublicField, NonPublicField, NonSerializedPublicField, NonSerializedNonPublicField, CollectionReadOnlyProperty );
#else
			TestNonPublicWritableMemberCore( target, PublicProperty, NonPublicProperty, PublicField, NonPublicField, CollectionReadOnlyProperty );
#endif // !NETFX_CORE && !SILVERLIGHT
		}

		[Test]
		public void TestNonPublicWritableMember_DataContract()
		{
			// includes issue33
			var target = new DataMamberClass();
			target.CollectionReadOnlyProperty.Add( 10 );
#if !NETFX_CORE && !SILVERLIGHT
			TestNonPublicWritableMemberCore( target, PublicProperty, NonPublicProperty, PublicField, NonPublicField, NonSerializedPublicField, NonSerializedNonPublicField, CollectionReadOnlyProperty );
#else
			TestNonPublicWritableMemberCore( target, PublicProperty, NonPublicProperty, PublicField, NonPublicField, CollectionReadOnlyProperty );
#endif // !NETFX_CORE && !SILVERLIGHT
		}

		private void TestNonPublicWritableMemberCore<T>( T original, params string[] expectedMemberNames )
		{
			var serializer = CreateTarget<T>( GetSerializationContext() );
			using ( var buffer = new MemoryStream() )
			{
				serializer.Pack( buffer, original );
				buffer.Position = 0;
				var actual = serializer.Unpack( buffer );

				foreach ( var memberName in expectedMemberNames )
				{
					Func<T, Object> getter = null;
#if !NETFX_CORE && !SILVERLIGHT
					var property = typeof( T ).GetProperty( memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic );
#elif NETFX_CORE
					var property = typeof( T ).GetRuntimeProperties().SingleOrDefault( p => p.Name == memberName );
#else
					var property = typeof( T ).GetProperty( memberName );
#endif
					if ( property != null )
					{
						getter = obj => property.GetValue( obj, null );
					}
					else
					{
#if !NETFX_CORE && !SILVERLIGHT
						var field =  typeof( T ).GetField( memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic );
#elif NETFX_CORE
						var field = typeof( T ).GetRuntimeFields().SingleOrDefault( f => f.Name == memberName );
#else
						var field = typeof( T ).GetField( memberName );
#endif
						if ( field == null )
						{
							Assert.Fail( memberName + " is not found." );
						}

						getter = obj => field.GetValue( obj );
					}

					Assert.That( getter( actual ), Is.EqualTo( getter( original ) ), typeof(T) + "." + memberName );
				}
			}
		}

#endregion -- ReadOnly / Private Members --

		public class HasInitOnlyField
		{
			public readonly string Field = "ABC";
		}

		public class HasGetOnlyProperty
		{
			public string Property { get { return "ABC"; } }
		}

		public class JustPackable : IPackable
		{
			public const string Dummy = "A";

			public int Int32Field { get; set; }

			public void PackToMessage( Packer packer, PackingOptions options )
			{
				packer.PackArrayHeader( 1 );
				packer.PackString( Dummy );
			}
		}

		public class JustUnpackable : IUnpackable
		{
			public const string Dummy = "1";

			public int Int32Field { get; set; }

			public void UnpackFromMessage( Unpacker unpacker )
			{
				var value = unpacker.UnpackSubtreeData();
				if ( value.IsArray )
				{
					Assert.That( value.AsList()[ 0 ] == 0, "{0} != \"[{1}]\"", value, 0 );
				}
				else if ( value.IsMap )
				{
					Assert.That( value.AsDictionary().First().Value == 0, "{0} != \"[{1}]\"", value, 0 );
				}
				else
				{
					Assert.Fail( "Unknown spec." );
				}

				this.Int32Field = Int32.Parse( Dummy );
			}
		}

		public class PackableUnpackable : IPackable, IUnpackable
		{
			public const string Dummy = "A";

			public int Int32Field { get; set; }

			public void PackToMessage( Packer packer, PackingOptions options )
			{
				packer.PackArrayHeader( 1 );
				packer.PackString( Dummy );
			}

			public void UnpackFromMessage( Unpacker unpacker )
			{
				Assert.That( unpacker.IsArrayHeader );
				var value = unpacker.UnpackSubtreeData();
				Assert.That( value.AsList()[ 0 ] == Dummy, "{0} != \"[{1}]\"", value, Dummy );
			}
		}

		public class CustomDateTimeSerealizer : MessagePackSerializer<DateTime>
		{
			private const byte _typeCodeForDateTimeForUs = 1;

			public CustomDateTimeSerealizer()
				: base( SerializationContext.Default ) {}

			protected internal override void PackToCore( Packer packer, DateTime objectTree )
			{
				byte[] data;
				if ( BitConverter.IsLittleEndian )
				{
					data = BitConverter.GetBytes( objectTree.ToUniversalTime().Ticks ).Reverse().ToArray();
				}
				else
				{
					data = BitConverter.GetBytes( objectTree.ToUniversalTime().Ticks );
				}

				packer.PackExtendedTypeValue( _typeCodeForDateTimeForUs, data );
			}

			protected internal override DateTime UnpackFromCore( Unpacker unpacker )
			{
				var ext = unpacker.LastReadData.AsMessagePackExtendedTypeObject();
				Assert.That( ext.TypeCode, Is.EqualTo( 1 ) );
				return new DateTime( BigEndianBinary.ToInt64( ext.Body, 0 ) ).ToUniversalTime();
			}
		}

		// Issue #25

		public class Person : IEnumerable<Person>
		{
			public string Name { get; set; }

			internal IEnumerable<Person> Children { get; set; }

			public IEnumerator<Person> GetEnumerator()
			{
				return Children.GetEnumerator();
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return GetEnumerator();
			}
		}

		public class PersonSerializer : MessagePackSerializer<Person>
		{
			public PersonSerializer()
				: base( SerializationContext.Default ) {}

			protected internal override void PackToCore( Packer packer, Person objectTree )
			{
				packer.PackMapHeader( 2 );
				packer.Pack( "Name" );
				packer.Pack( objectTree.Name );
				packer.Pack( "Children" );
				if ( objectTree.Children == null )
				{
					packer.PackNull();
				}
				else
				{
					this.PackPeople( packer, objectTree.Children );
				}
			}

			internal void PackPeople( Packer packer, IEnumerable<Person> people )
			{
				var children = people.ToArray();

				packer.PackArrayHeader( children.Length );
				foreach ( var child in children )
				{
					this.PackTo( packer, child );
				}
			}

			protected internal override Person UnpackFromCore( Unpacker unpacker )
			{
				Assert.That( unpacker.IsMapHeader );
				Assert.That( unpacker.ItemsCount, Is.EqualTo( 2 ) );
				var person = new Person();
				for ( int i = 0; i < 2; i++ )
				{
					string key;
					Assert.That( unpacker.ReadString( out key ) );
					switch ( key )
					{
						case "Name":
						{

							string name;
							Assert.That( unpacker.ReadString( out name ) );
							person.Name = name;
							break;
						}
						case "Children":
						{
							Assert.That( unpacker.Read() );
							if ( !unpacker.LastReadData.IsNil )
							{
								person.Children = this.UnpackPeople( unpacker );
							}
							break;
						}
					}
				}

				return person;
			}

			internal IEnumerable<Person> UnpackPeople( Unpacker unpacker )
			{
				Assert.That( unpacker.IsArrayHeader );
				var itemsCount = ( int )unpacker.ItemsCount;
				var people = new List<Person>( itemsCount );
				for ( int i = 0; i < itemsCount; i++ )
				{
					people.Add( this.UnpackFrom( unpacker ) );
				}

				return people;
			}
		}

		public class ChildrenSerializer : MessagePackSerializer<IEnumerable<Person>>
		{
			private readonly PersonSerializer _personSerializer = new PersonSerializer();

			public ChildrenSerializer()
				: base( SerializationContext.Default ) {}

			protected internal override void PackToCore( Packer packer, IEnumerable<Person> objectTree )
			{
				if ( objectTree is Person )
				{
					this._personSerializer.PackTo( packer, objectTree as Person );
				}
				else
				{
					this._personSerializer.PackPeople( packer, objectTree );
				}
			}

			protected internal override IEnumerable<Person> UnpackFromCore( Unpacker unpacker )
			{
				return this._personSerializer.UnpackPeople( unpacker );
			}
		}

		// issue #63
		[Test]
		public void TestManyMembers()
		{
			var serializer = this.CreateTarget<WithManyMembers>( GetSerializationContext() );
			var target = new WithManyMembers();
			using ( var buffer = new MemoryStream() )
			{
				serializer.Pack( buffer, target );
				buffer.Position = 0;
				var result = serializer.Unpack( buffer );
				Assert.That( result, Is.EqualTo( target ) );
			}
		}

#pragma warning disable 659
		public class WithManyMembers
		{
			private readonly int[] _backingField = Enumerable.Range( 0, SByte.MaxValue + 2 ).ToArray();

			public int Member0
			{
				get { return this._backingField[ 0 ]; }
				set { this._backingField[ 0 ] = value; }
			}
			public int Member1
			{
				get { return this._backingField[ 1 ]; }
				set { this._backingField[ 1 ] = value; }
			}
			public int Member2
			{
				get { return this._backingField[ 2 ]; }
				set { this._backingField[ 2 ] = value; }
			}
			public int Member3
			{
				get { return this._backingField[ 3 ]; }
				set { this._backingField[ 3 ] = value; }
			}
			public int Member4
			{
				get { return this._backingField[ 4 ]; }
				set { this._backingField[ 4 ] = value; }
			}
			public int Member5
			{
				get { return this._backingField[ 5 ]; }
				set { this._backingField[ 5 ] = value; }
			}
			public int Member6
			{
				get { return this._backingField[ 6 ]; }
				set { this._backingField[ 6 ] = value; }
			}
			public int Member7
			{
				get { return this._backingField[ 7 ]; }
				set { this._backingField[ 7 ] = value; }
			}
			public int Member8
			{
				get { return this._backingField[ 8 ]; }
				set { this._backingField[ 8 ] = value; }
			}
			public int Member9
			{
				get { return this._backingField[ 9 ]; }
				set { this._backingField[ 9 ] = value; }
			}
			public int Member10
			{
				get { return this._backingField[ 10 ]; }
				set { this._backingField[ 10 ] = value; }
			}
			public int Member11
			{
				get { return this._backingField[ 11 ]; }
				set { this._backingField[ 11 ] = value; }
			}
			public int Member12
			{
				get { return this._backingField[ 12 ]; }
				set { this._backingField[ 12 ] = value; }
			}
			public int Member13
			{
				get { return this._backingField[ 13 ]; }
				set { this._backingField[ 13 ] = value; }
			}
			public int Member14
			{
				get { return this._backingField[ 14 ]; }
				set { this._backingField[ 14 ] = value; }
			}
			public int Member15
			{
				get { return this._backingField[ 15 ]; }
				set { this._backingField[ 15 ] = value; }
			}
			public int Member16
			{
				get { return this._backingField[ 16 ]; }
				set { this._backingField[ 16 ] = value; }
			}
			public int Member17
			{
				get { return this._backingField[ 17 ]; }
				set { this._backingField[ 17 ] = value; }
			}
			public int Member18
			{
				get { return this._backingField[ 18 ]; }
				set { this._backingField[ 18 ] = value; }
			}
			public int Member19
			{
				get { return this._backingField[ 19 ]; }
				set { this._backingField[ 19 ] = value; }
			}
			public int Member20
			{
				get { return this._backingField[ 20 ]; }
				set { this._backingField[ 20 ] = value; }
			}
			public int Member21
			{
				get { return this._backingField[ 21 ]; }
				set { this._backingField[ 21 ] = value; }
			}
			public int Member22
			{
				get { return this._backingField[ 22 ]; }
				set { this._backingField[ 22 ] = value; }
			}
			public int Member23
			{
				get { return this._backingField[ 23 ]; }
				set { this._backingField[ 23 ] = value; }
			}
			public int Member24
			{
				get { return this._backingField[ 24 ]; }
				set { this._backingField[ 24 ] = value; }
			}
			public int Member25
			{
				get { return this._backingField[ 25 ]; }
				set { this._backingField[ 25 ] = value; }
			}
			public int Member26
			{
				get { return this._backingField[ 26 ]; }
				set { this._backingField[ 26 ] = value; }
			}
			public int Member27
			{
				get { return this._backingField[ 27 ]; }
				set { this._backingField[ 27 ] = value; }
			}
			public int Member28
			{
				get { return this._backingField[ 28 ]; }
				set { this._backingField[ 28 ] = value; }
			}
			public int Member29
			{
				get { return this._backingField[ 29 ]; }
				set { this._backingField[ 29 ] = value; }
			}
			public int Member30
			{
				get { return this._backingField[ 30 ]; }
				set { this._backingField[ 30 ] = value; }
			}
			public int Member31
			{
				get { return this._backingField[ 31 ]; }
				set { this._backingField[ 31 ] = value; }
			}
			public int Member32
			{
				get { return this._backingField[ 32 ]; }
				set { this._backingField[ 32 ] = value; }
			}
			public int Member33
			{
				get { return this._backingField[ 33 ]; }
				set { this._backingField[ 33 ] = value; }
			}
			public int Member34
			{
				get { return this._backingField[ 34 ]; }
				set { this._backingField[ 34 ] = value; }
			}
			public int Member35
			{
				get { return this._backingField[ 35 ]; }
				set { this._backingField[ 35 ] = value; }
			}
			public int Member36
			{
				get { return this._backingField[ 36 ]; }
				set { this._backingField[ 36 ] = value; }
			}
			public int Member37
			{
				get { return this._backingField[ 37 ]; }
				set { this._backingField[ 37 ] = value; }
			}
			public int Member38
			{
				get { return this._backingField[ 38 ]; }
				set { this._backingField[ 38 ] = value; }
			}
			public int Member39
			{
				get { return this._backingField[ 39 ]; }
				set { this._backingField[ 39 ] = value; }
			}
			public int Member40
			{
				get { return this._backingField[ 40 ]; }
				set { this._backingField[ 40 ] = value; }
			}
			public int Member41
			{
				get { return this._backingField[ 41 ]; }
				set { this._backingField[ 41 ] = value; }
			}
			public int Member42
			{
				get { return this._backingField[ 42 ]; }
				set { this._backingField[ 42 ] = value; }
			}
			public int Member43
			{
				get { return this._backingField[ 43 ]; }
				set { this._backingField[ 43 ] = value; }
			}
			public int Member44
			{
				get { return this._backingField[ 44 ]; }
				set { this._backingField[ 44 ] = value; }
			}
			public int Member45
			{
				get { return this._backingField[ 45 ]; }
				set { this._backingField[ 45 ] = value; }
			}
			public int Member46
			{
				get { return this._backingField[ 46 ]; }
				set { this._backingField[ 46 ] = value; }
			}
			public int Member47
			{
				get { return this._backingField[ 47 ]; }
				set { this._backingField[ 47 ] = value; }
			}
			public int Member48
			{
				get { return this._backingField[ 48 ]; }
				set { this._backingField[ 48 ] = value; }
			}
			public int Member49
			{
				get { return this._backingField[ 49 ]; }
				set { this._backingField[ 49 ] = value; }
			}
			public int Member50
			{
				get { return this._backingField[ 50 ]; }
				set { this._backingField[ 50 ] = value; }
			}
			public int Member51
			{
				get { return this._backingField[ 51 ]; }
				set { this._backingField[ 51 ] = value; }
			}
			public int Member52
			{
				get { return this._backingField[ 52 ]; }
				set { this._backingField[ 52 ] = value; }
			}
			public int Member53
			{
				get { return this._backingField[ 53 ]; }
				set { this._backingField[ 53 ] = value; }
			}
			public int Member54
			{
				get { return this._backingField[ 54 ]; }
				set { this._backingField[ 54 ] = value; }
			}
			public int Member55
			{
				get { return this._backingField[ 55 ]; }
				set { this._backingField[ 55 ] = value; }
			}
			public int Member56
			{
				get { return this._backingField[ 56 ]; }
				set { this._backingField[ 56 ] = value; }
			}
			public int Member57
			{
				get { return this._backingField[ 57 ]; }
				set { this._backingField[ 57 ] = value; }
			}
			public int Member58
			{
				get { return this._backingField[ 58 ]; }
				set { this._backingField[ 58 ] = value; }
			}
			public int Member59
			{
				get { return this._backingField[ 59 ]; }
				set { this._backingField[ 59 ] = value; }
			}
			public int Member60
			{
				get { return this._backingField[ 60 ]; }
				set { this._backingField[ 60 ] = value; }
			}
			public int Member61
			{
				get { return this._backingField[ 61 ]; }
				set { this._backingField[ 61 ] = value; }
			}
			public int Member62
			{
				get { return this._backingField[ 62 ]; }
				set { this._backingField[ 62 ] = value; }
			}
			public int Member63
			{
				get { return this._backingField[ 63 ]; }
				set { this._backingField[ 63 ] = value; }
			}
			public int Member64
			{
				get { return this._backingField[ 64 ]; }
				set { this._backingField[ 64 ] = value; }
			}
			public int Member65
			{
				get { return this._backingField[ 65 ]; }
				set { this._backingField[ 65 ] = value; }
			}
			public int Member66
			{
				get { return this._backingField[ 66 ]; }
				set { this._backingField[ 66 ] = value; }
			}
			public int Member67
			{
				get { return this._backingField[ 67 ]; }
				set { this._backingField[ 67 ] = value; }
			}
			public int Member68
			{
				get { return this._backingField[ 68 ]; }
				set { this._backingField[ 68 ] = value; }
			}
			public int Member69
			{
				get { return this._backingField[ 69 ]; }
				set { this._backingField[ 69 ] = value; }
			}
			public int Member70
			{
				get { return this._backingField[ 70 ]; }
				set { this._backingField[ 70 ] = value; }
			}
			public int Member71
			{
				get { return this._backingField[ 71 ]; }
				set { this._backingField[ 71 ] = value; }
			}
			public int Member72
			{
				get { return this._backingField[ 72 ]; }
				set { this._backingField[ 72 ] = value; }
			}
			public int Member73
			{
				get { return this._backingField[ 73 ]; }
				set { this._backingField[ 73 ] = value; }
			}
			public int Member74
			{
				get { return this._backingField[ 74 ]; }
				set { this._backingField[ 74 ] = value; }
			}
			public int Member75
			{
				get { return this._backingField[ 75 ]; }
				set { this._backingField[ 75 ] = value; }
			}
			public int Member76
			{
				get { return this._backingField[ 76 ]; }
				set { this._backingField[ 76 ] = value; }
			}
			public int Member77
			{
				get { return this._backingField[ 77 ]; }
				set { this._backingField[ 77 ] = value; }
			}
			public int Member78
			{
				get { return this._backingField[ 78 ]; }
				set { this._backingField[ 78 ] = value; }
			}
			public int Member79
			{
				get { return this._backingField[ 79 ]; }
				set { this._backingField[ 79 ] = value; }
			}
			public int Member80
			{
				get { return this._backingField[ 80 ]; }
				set { this._backingField[ 80 ] = value; }
			}
			public int Member81
			{
				get { return this._backingField[ 81 ]; }
				set { this._backingField[ 81 ] = value; }
			}
			public int Member82
			{
				get { return this._backingField[ 82 ]; }
				set { this._backingField[ 82 ] = value; }
			}
			public int Member83
			{
				get { return this._backingField[ 83 ]; }
				set { this._backingField[ 83 ] = value; }
			}
			public int Member84
			{
				get { return this._backingField[ 84 ]; }
				set { this._backingField[ 84 ] = value; }
			}
			public int Member85
			{
				get { return this._backingField[ 85 ]; }
				set { this._backingField[ 85 ] = value; }
			}
			public int Member86
			{
				get { return this._backingField[ 86 ]; }
				set { this._backingField[ 86 ] = value; }
			}
			public int Member87
			{
				get { return this._backingField[ 87 ]; }
				set { this._backingField[ 87 ] = value; }
			}
			public int Member88
			{
				get { return this._backingField[ 88 ]; }
				set { this._backingField[ 88 ] = value; }
			}
			public int Member89
			{
				get { return this._backingField[ 89 ]; }
				set { this._backingField[ 89 ] = value; }
			}
			public int Member90
			{
				get { return this._backingField[ 90 ]; }
				set { this._backingField[ 90 ] = value; }
			}
			public int Member91
			{
				get { return this._backingField[ 91 ]; }
				set { this._backingField[ 91 ] = value; }
			}
			public int Member92
			{
				get { return this._backingField[ 92 ]; }
				set { this._backingField[ 92 ] = value; }
			}
			public int Member93
			{
				get { return this._backingField[ 93 ]; }
				set { this._backingField[ 93 ] = value; }
			}
			public int Member94
			{
				get { return this._backingField[ 94 ]; }
				set { this._backingField[ 94 ] = value; }
			}
			public int Member95
			{
				get { return this._backingField[ 95 ]; }
				set { this._backingField[ 95 ] = value; }
			}
			public int Member96
			{
				get { return this._backingField[ 96 ]; }
				set { this._backingField[ 96 ] = value; }
			}
			public int Member97
			{
				get { return this._backingField[ 97 ]; }
				set { this._backingField[ 97 ] = value; }
			}
			public int Member98
			{
				get { return this._backingField[ 98 ]; }
				set { this._backingField[ 98 ] = value; }
			}
			public int Member99
			{
				get { return this._backingField[ 99 ]; }
				set { this._backingField[ 99 ] = value; }
			}
			public int Member100
			{
				get { return this._backingField[ 100 ]; }
				set { this._backingField[ 100 ] = value; }
			}
			public int Member101
			{
				get { return this._backingField[ 101 ]; }
				set { this._backingField[ 101 ] = value; }
			}
			public int Member102
			{
				get { return this._backingField[ 102 ]; }
				set { this._backingField[ 102 ] = value; }
			}
			public int Member103
			{
				get { return this._backingField[ 103 ]; }
				set { this._backingField[ 103 ] = value; }
			}
			public int Member104
			{
				get { return this._backingField[ 104 ]; }
				set { this._backingField[ 104 ] = value; }
			}
			public int Member105
			{
				get { return this._backingField[ 105 ]; }
				set { this._backingField[ 105 ] = value; }
			}
			public int Member106
			{
				get { return this._backingField[ 106 ]; }
				set { this._backingField[ 106 ] = value; }
			}
			public int Member107
			{
				get { return this._backingField[ 107 ]; }
				set { this._backingField[ 107 ] = value; }
			}
			public int Member108
			{
				get { return this._backingField[ 108 ]; }
				set { this._backingField[ 108 ] = value; }
			}
			public int Member109
			{
				get { return this._backingField[ 109 ]; }
				set { this._backingField[ 109 ] = value; }
			}
			public int Member110
			{
				get { return this._backingField[ 110 ]; }
				set { this._backingField[ 110 ] = value; }
			}
			public int Member111
			{
				get { return this._backingField[ 111 ]; }
				set { this._backingField[ 111 ] = value; }
			}
			public int Member112
			{
				get { return this._backingField[ 112 ]; }
				set { this._backingField[ 112 ] = value; }
			}
			public int Member113
			{
				get { return this._backingField[ 113 ]; }
				set { this._backingField[ 113 ] = value; }
			}
			public int Member114
			{
				get { return this._backingField[ 114 ]; }
				set { this._backingField[ 114 ] = value; }
			}
			public int Member115
			{
				get { return this._backingField[ 115 ]; }
				set { this._backingField[ 115 ] = value; }
			}
			public int Member116
			{
				get { return this._backingField[ 116 ]; }
				set { this._backingField[ 116 ] = value; }
			}
			public int Member117
			{
				get { return this._backingField[ 117 ]; }
				set { this._backingField[ 117 ] = value; }
			}
			public int Member118
			{
				get { return this._backingField[ 118 ]; }
				set { this._backingField[ 118 ] = value; }
			}
			public int Member119
			{
				get { return this._backingField[ 119 ]; }
				set { this._backingField[ 119 ] = value; }
			}
			public int Member120
			{
				get { return this._backingField[ 120 ]; }
				set { this._backingField[ 120 ] = value; }
			}
			public int Member121
			{
				get { return this._backingField[ 121 ]; }
				set { this._backingField[ 121 ] = value; }
			}
			public int Member122
			{
				get { return this._backingField[ 122 ]; }
				set { this._backingField[ 122 ] = value; }
			}
			public int Member123
			{
				get { return this._backingField[ 123 ]; }
				set { this._backingField[ 123 ] = value; }
			}
			public int Member124
			{
				get { return this._backingField[ 124 ]; }
				set { this._backingField[ 124 ] = value; }
			}
			public int Member125
			{
				get { return this._backingField[ 125 ]; }
				set { this._backingField[ 125 ] = value; }
			}
			public int Member126
			{
				get { return this._backingField[ 126 ]; }
				set { this._backingField[ 126 ] = value; }
			}
			public int Member127
			{
				get { return this._backingField[ 127 ]; }
				set { this._backingField[ 127 ] = value; }
			}
			public int Member128
			{
				get { return this._backingField[ 128 ]; }
				set { this._backingField[ 128 ] = value; }
			}

			public override bool Equals( object obj )
			{
				var other = obj as WithManyMembers;
				if ( other == null )
				{
					return false;
				}

				return this._backingField == other._backingField || this._backingField.SequenceEqual( other._backingField );
			}
		}
#pragma warning restore 659

		[Test]
		public void TestNullField()
		{
			this.TestCoreWithAutoVerify( default( object ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestNullFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( default( object ), 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestNullFieldNull()
		{
			this.TestCoreWithAutoVerify( default( Object ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestNullFieldArrayNull()
		{
			this.TestCoreWithAutoVerify( default( Object[] ), this.GetSerializationContext() );
		}	
		
		[Test]
		public void TestTrueField()
		{
			this.TestCoreWithAutoVerify( true, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestTrueFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( true, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestFalseField()
		{
			this.TestCoreWithAutoVerify( false, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestFalseFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( false, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestTinyByteField()
		{
			this.TestCoreWithAutoVerify( 1, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestTinyByteFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( 1, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestByteField()
		{
			this.TestCoreWithAutoVerify( 0x80, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestByteFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( 0x80, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestMaxByteField()
		{
			this.TestCoreWithAutoVerify( 0xff, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestMaxByteFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( 0xff, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestTinyUInt16Field()
		{
			this.TestCoreWithAutoVerify( 0x100, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestTinyUInt16FieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( 0x100, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestMaxUInt16Field()
		{
			this.TestCoreWithAutoVerify( 0xffff, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestMaxUInt16FieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( 0xffff, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestTinyInt32Field()
		{
			this.TestCoreWithAutoVerify( 0x10000, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestTinyInt32FieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( 0x10000, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestMaxInt32Field()
		{
			this.TestCoreWithAutoVerify( Int32.MaxValue, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestMaxInt32FieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( Int32.MaxValue, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestMinInt32Field()
		{
			this.TestCoreWithAutoVerify( Int32.MinValue, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestMinInt32FieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( Int32.MinValue, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestTinyInt64Field()
		{
			this.TestCoreWithAutoVerify( 0x100000000, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestTinyInt64FieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( 0x100000000, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestMaxInt64Field()
		{
			this.TestCoreWithAutoVerify( Int64.MaxValue, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestMaxInt64FieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( Int64.MaxValue, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestMinInt64Field()
		{
			this.TestCoreWithAutoVerify( Int64.MinValue, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestMinInt64FieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( Int64.MinValue, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestDateTimeField()
		{
			this.TestCoreWithAutoVerify( DateTime.UtcNow, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestDateTimeFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( DateTime.UtcNow, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestDateTimeOffsetField()
		{
			this.TestCoreWithAutoVerify( DateTimeOffset.UtcNow, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestDateTimeOffsetFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( DateTimeOffset.UtcNow, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestUriField()
		{
			this.TestCoreWithAutoVerify( new Uri( "http://example.com/" ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestUriFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new Uri( "http://example.com/" ), 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestUriFieldNull()
		{
			this.TestCoreWithAutoVerify( default( Uri ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestUriFieldArrayNull()
		{
			this.TestCoreWithAutoVerify( default( Uri[] ), this.GetSerializationContext() );
		}	
		
		[Test]
		public void TestVersionField()
		{
			this.TestCoreWithAutoVerify( new Version( 1, 2, 3, 4 ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestVersionFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new Version( 1, 2, 3, 4 ), 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestVersionFieldNull()
		{
			this.TestCoreWithAutoVerify( default( Version ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestVersionFieldArrayNull()
		{
			this.TestCoreWithAutoVerify( default( Version[] ), this.GetSerializationContext() );
		}	
		
		[Test]
		public void TestFILETIMEField()
		{
			this.TestCoreWithAutoVerify( ToFileTime( DateTime.UtcNow ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestFILETIMEFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( ToFileTime( DateTime.UtcNow ), 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestTimeSpanField()
		{
			this.TestCoreWithAutoVerify( TimeSpan.FromMilliseconds( 123456789 ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestTimeSpanFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( TimeSpan.FromMilliseconds( 123456789 ), 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestGuidField()
		{
			this.TestCoreWithAutoVerify( Guid.NewGuid(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestGuidFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( Guid.NewGuid(), 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestCharField()
		{
			this.TestCoreWithAutoVerify( '　', this.GetSerializationContext() );
		}
		
		[Test]
		public void TestCharFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( '　', 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestDecimalField()
		{
			this.TestCoreWithAutoVerify( 123456789.0987654321m, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestDecimalFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( 123456789.0987654321m, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
#if !NETFX_35 && !WINDOWS_PHONE
		[Test]
		public void TestBigIntegerField()
		{
			this.TestCoreWithAutoVerify( new BigInteger( UInt64.MaxValue ) + UInt64.MaxValue, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestBigIntegerFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new BigInteger( UInt64.MaxValue ) + UInt64.MaxValue, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
#endif // !NETFX_35 && !WINDOWS_PHONE
#if !NETFX_35 && !WINDOWS_PHONE
		[Test]
		public void TestComplexField()
		{
			this.TestCoreWithAutoVerify( new Complex( 1.3, 2.4 ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestComplexFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new Complex( 1.3, 2.4 ), 2 ).ToArray(), this.GetSerializationContext() );
		}
		
#endif // !NETFX_35 && !WINDOWS_PHONE
		[Test]
		public void TestDictionaryEntryField()
		{
			this.TestCoreWithAutoVerify( new DictionaryEntry( new MessagePackObject( "Key" ), new MessagePackObject( "Value" ) ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestDictionaryEntryFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new DictionaryEntry( new MessagePackObject( "Key" ), new MessagePackObject( "Value" ) ), 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestKeyValuePairStringDateTimeOffsetField()
		{
			this.TestCoreWithAutoVerify( new KeyValuePair<String, DateTimeOffset>( "Key", DateTimeOffset.UtcNow ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestKeyValuePairStringDateTimeOffsetFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new KeyValuePair<String, DateTimeOffset>( "Key", DateTimeOffset.UtcNow ), 2 ).ToArray(), this.GetSerializationContext() );
		}
		
#if !NETFX_35 && !WINDOWS_PHONE
		[Test]
		public void TestKeyValuePairStringComplexField()
		{
			this.TestCoreWithAutoVerify( new KeyValuePair<String, Complex>( "Key", new Complex( 1.3, 2.4 ) ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestKeyValuePairStringComplexFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new KeyValuePair<String, Complex>( "Key", new Complex( 1.3, 2.4 ) ), 2 ).ToArray(), this.GetSerializationContext() );
		}
		
#endif // !NETFX_35 && !WINDOWS_PHONE
		[Test]
		public void TestStringField()
		{
			this.TestCoreWithAutoVerify( "StringValue", this.GetSerializationContext() );
		}
		
		[Test]
		public void TestStringFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( "StringValue", 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestStringFieldNull()
		{
			this.TestCoreWithAutoVerify( default( String ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestStringFieldArrayNull()
		{
			this.TestCoreWithAutoVerify( default( String[] ), this.GetSerializationContext() );
		}	
		
		[Test]
		public void TestByteArrayField()
		{
			this.TestCoreWithAutoVerify( new Byte[]{ 1, 2, 3, 4 }, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestByteArrayFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new Byte[]{ 1, 2, 3, 4 }, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestByteArrayFieldNull()
		{
			this.TestCoreWithAutoVerify( default( Byte[] ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestByteArrayFieldArrayNull()
		{
			this.TestCoreWithAutoVerify( default( Byte[][] ), this.GetSerializationContext() );
		}	
		
		[Test]
		public void TestCharArrayField()
		{
			this.TestCoreWithAutoVerify( "ABCD".ToCharArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestCharArrayFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( "ABCD".ToCharArray(), 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestCharArrayFieldNull()
		{
			this.TestCoreWithAutoVerify( default( Char[] ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestCharArrayFieldArrayNull()
		{
			this.TestCoreWithAutoVerify( default( Char[][] ), this.GetSerializationContext() );
		}	
		
		[Test]
		public void TestArraySegmentByteField()
		{
			this.TestCoreWithAutoVerify( new ArraySegment<Byte>( new Byte[]{ 1, 2, 3, 4 } ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestArraySegmentByteFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new ArraySegment<Byte>( new Byte[]{ 1, 2, 3, 4 } ), 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestArraySegmentInt32Field()
		{
			this.TestCoreWithAutoVerify( new ArraySegment<Int32>( new Int32[]{ 1, 2, 3, 4 } ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestArraySegmentInt32FieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new ArraySegment<Int32>( new Int32[]{ 1, 2, 3, 4 } ), 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestArraySegmentDecimalField()
		{
			this.TestCoreWithAutoVerify( new ArraySegment<Decimal>( new Decimal[]{ 1, 2, 3, 4 } ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestArraySegmentDecimalFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new ArraySegment<Decimal>( new Decimal[]{ 1, 2, 3, 4 } ), 2 ).ToArray(), this.GetSerializationContext() );
		}
		
#if !NETFX_35
		[Test]
		public void TestTuple_Int32_String_MessagePackObject_ObjectField()
		{
			this.TestCoreWithAutoVerify( new Tuple<Int32, String, MessagePackObject, Object>( 1, "ABC", new MessagePackObject( "abc" ), new MessagePackObject( "123" ) ) , this.GetSerializationContext() );
		}
		
		[Test]
		public void TestTuple_Int32_String_MessagePackObject_ObjectFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new Tuple<Int32, String, MessagePackObject, Object>( 1, "ABC", new MessagePackObject( "abc" ), new MessagePackObject( "123" ) ) , 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestTuple_Int32_String_MessagePackObject_ObjectFieldNull()
		{
			this.TestCoreWithAutoVerify( default( System.Tuple<System.Int32, System.String, MsgPack.MessagePackObject, System.Object> ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestTuple_Int32_String_MessagePackObject_ObjectFieldArrayNull()
		{
			this.TestCoreWithAutoVerify( default( System.Tuple<System.Int32, System.String, MsgPack.MessagePackObject, System.Object>[] ), this.GetSerializationContext() );
		}	
		
#endif // !NETFX_35
		[Test]
		public void TestImage_Field()
		{
			this.TestCoreWithAutoVerify( new Image(){ uri = "http://example.com/logo.png", title = "logo", width = 160, height = 120, size = 13612 }, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestImage_FieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new Image(){ uri = "http://example.com/logo.png", title = "logo", width = 160, height = 120, size = 13612 }, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestImage_FieldNull()
		{
			this.TestCoreWithAutoVerify( default( MsgPack.Image ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestImage_FieldArrayNull()
		{
			this.TestCoreWithAutoVerify( default( MsgPack.Image[] ), this.GetSerializationContext() );
		}	
		
		[Test]
		public void TestListDateTimeField()
		{
			this.TestCoreWithAutoVerify( new List<DateTime>(){ DateTime.UtcNow.Subtract( TimeSpan.FromDays( 1 ) ), DateTime.UtcNow }, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestListDateTimeFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new List<DateTime>(){ DateTime.UtcNow.Subtract( TimeSpan.FromDays( 1 ) ), DateTime.UtcNow }, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestListDateTimeFieldNull()
		{
			this.TestCoreWithAutoVerify( default( List<DateTime> ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestListDateTimeFieldArrayNull()
		{
			this.TestCoreWithAutoVerify( default( List<DateTime>[] ), this.GetSerializationContext() );
		}	
		
		[Test]
		public void TestDictionaryStringDateTimeField()
		{
			this.TestCoreWithAutoVerify( new Dictionary<String, DateTime>(){ { "Yesterday", DateTime.UtcNow.Subtract( TimeSpan.FromDays( 1 ) ) }, { "Today", DateTime.UtcNow } }, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestDictionaryStringDateTimeFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new Dictionary<String, DateTime>(){ { "Yesterday", DateTime.UtcNow.Subtract( TimeSpan.FromDays( 1 ) ) }, { "Today", DateTime.UtcNow } }, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestDictionaryStringDateTimeFieldNull()
		{
			this.TestCoreWithAutoVerify( default( Dictionary<String, DateTime> ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestDictionaryStringDateTimeFieldArrayNull()
		{
			this.TestCoreWithAutoVerify( default( Dictionary<String, DateTime>[] ), this.GetSerializationContext() );
		}	
		
		[Test]
		public void TestCollectionDateTimeField()
		{
			this.TestCoreWithAutoVerify( new Collection<DateTime>(){ DateTime.UtcNow.Subtract( TimeSpan.FromDays( 1 ) ), DateTime.UtcNow }, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestCollectionDateTimeFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new Collection<DateTime>(){ DateTime.UtcNow.Subtract( TimeSpan.FromDays( 1 ) ), DateTime.UtcNow }, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestCollectionDateTimeFieldNull()
		{
			this.TestCoreWithAutoVerify( default( Collection<DateTime> ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestCollectionDateTimeFieldArrayNull()
		{
			this.TestCoreWithAutoVerify( default( Collection<DateTime>[] ), this.GetSerializationContext() );
		}	
		
		[Test]
		public void TestStringKeyedCollection_DateTimeField()
		{
			this.TestCoreWithAutoVerify( new StringKeyedCollection<DateTime>(){ DateTime.UtcNow.Subtract( TimeSpan.FromDays( 1 ) ), DateTime.UtcNow }, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestStringKeyedCollection_DateTimeFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new StringKeyedCollection<DateTime>(){ DateTime.UtcNow.Subtract( TimeSpan.FromDays( 1 ) ), DateTime.UtcNow }, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestStringKeyedCollection_DateTimeFieldNull()
		{
			this.TestCoreWithAutoVerify( default( MsgPack.Serialization.StringKeyedCollection<System.DateTime> ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestStringKeyedCollection_DateTimeFieldArrayNull()
		{
			this.TestCoreWithAutoVerify( default( MsgPack.Serialization.StringKeyedCollection<System.DateTime>[] ), this.GetSerializationContext() );
		}	
		
#if !NETFX_35
		[Test]
		public void TestObservableCollectionDateTimeField()
		{
			this.TestCoreWithAutoVerify( new ObservableCollection<DateTime>(){ DateTime.UtcNow.Subtract( TimeSpan.FromDays( 1 ) ), DateTime.UtcNow }, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestObservableCollectionDateTimeFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new ObservableCollection<DateTime>(){ DateTime.UtcNow.Subtract( TimeSpan.FromDays( 1 ) ), DateTime.UtcNow }, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestObservableCollectionDateTimeFieldNull()
		{
			this.TestCoreWithAutoVerify( default( ObservableCollection<DateTime> ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestObservableCollectionDateTimeFieldArrayNull()
		{
			this.TestCoreWithAutoVerify( default( ObservableCollection<DateTime>[] ), this.GetSerializationContext() );
		}	
		
#endif // !NETFX_35
		[Test]
		public void TestHashSetDateTimeField()
		{
			this.TestCoreWithAutoVerify( new HashSet<DateTime>(){ DateTime.UtcNow.Subtract( TimeSpan.FromDays( 1 ) ), DateTime.UtcNow }, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestHashSetDateTimeFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new HashSet<DateTime>(){ DateTime.UtcNow.Subtract( TimeSpan.FromDays( 1 ) ), DateTime.UtcNow }, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestHashSetDateTimeFieldNull()
		{
			this.TestCoreWithAutoVerify( default( HashSet<DateTime> ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestHashSetDateTimeFieldArrayNull()
		{
			this.TestCoreWithAutoVerify( default( HashSet<DateTime>[] ), this.GetSerializationContext() );
		}	
		
		[Test]
		public void TestICollectionDateTimeField()
		{
			this.TestCoreWithAutoVerify( new SimpleCollection<DateTime>(){ DateTime.UtcNow.Subtract( TimeSpan.FromDays( 1 ) ), DateTime.UtcNow }, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestICollectionDateTimeFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new SimpleCollection<DateTime>(){ DateTime.UtcNow.Subtract( TimeSpan.FromDays( 1 ) ), DateTime.UtcNow }, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestICollectionDateTimeFieldNull()
		{
			this.TestCoreWithAutoVerify( default( ICollection<DateTime> ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestICollectionDateTimeFieldArrayNull()
		{
			this.TestCoreWithAutoVerify( default( ICollection<DateTime>[] ), this.GetSerializationContext() );
		}	
		
#if !NETFX_35
		[Test]
		public void TestISetDateTimeField()
		{
			this.TestCoreWithAutoVerify( new HashSet<DateTime>(){ DateTime.UtcNow.Subtract( TimeSpan.FromDays( 1 ) ), DateTime.UtcNow }, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestISetDateTimeFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new HashSet<DateTime>(){ DateTime.UtcNow.Subtract( TimeSpan.FromDays( 1 ) ), DateTime.UtcNow }, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestISetDateTimeFieldNull()
		{
			this.TestCoreWithAutoVerify( default( ISet<DateTime> ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestISetDateTimeFieldArrayNull()
		{
			this.TestCoreWithAutoVerify( default( ISet<DateTime>[] ), this.GetSerializationContext() );
		}	
		
#endif // !NETFX_35
		[Test]
		public void TestIListDateTimeField()
		{
			this.TestCoreWithAutoVerify( new List<DateTime>(){ DateTime.UtcNow.Subtract( TimeSpan.FromDays( 1 ) ), DateTime.UtcNow }, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestIListDateTimeFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new List<DateTime>(){ DateTime.UtcNow.Subtract( TimeSpan.FromDays( 1 ) ), DateTime.UtcNow }, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestIListDateTimeFieldNull()
		{
			this.TestCoreWithAutoVerify( default( IList<DateTime> ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestIListDateTimeFieldArrayNull()
		{
			this.TestCoreWithAutoVerify( default( IList<DateTime>[] ), this.GetSerializationContext() );
		}	
		
		[Test]
		public void TestIDictionaryStringDateTimeField()
		{
			this.TestCoreWithAutoVerify( new Dictionary<String, DateTime>(){ { "Yesterday", DateTime.UtcNow.Subtract( TimeSpan.FromDays( 1 ) ) }, { "Today", DateTime.UtcNow } }, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestIDictionaryStringDateTimeFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new Dictionary<String, DateTime>(){ { "Yesterday", DateTime.UtcNow.Subtract( TimeSpan.FromDays( 1 ) ) }, { "Today", DateTime.UtcNow } }, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestIDictionaryStringDateTimeFieldNull()
		{
			this.TestCoreWithAutoVerify( default( IDictionary<String, DateTime> ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestIDictionaryStringDateTimeFieldArrayNull()
		{
			this.TestCoreWithAutoVerify( default( IDictionary<String, DateTime>[] ), this.GetSerializationContext() );
		}	
		
		[Test]
		public void TestAddOnlyCollection_DateTimeField()
		{
			this.TestCoreWithAutoVerify( new AddOnlyCollection<DateTime>(){ DateTime.UtcNow.Subtract( TimeSpan.FromDays( 1 ) ), DateTime.UtcNow }, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestAddOnlyCollection_DateTimeFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new AddOnlyCollection<DateTime>(){ DateTime.UtcNow.Subtract( TimeSpan.FromDays( 1 ) ), DateTime.UtcNow }, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestAddOnlyCollection_DateTimeFieldNull()
		{
			this.TestCoreWithAutoVerify( default( MsgPack.Serialization.AddOnlyCollection<System.DateTime> ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestAddOnlyCollection_DateTimeFieldArrayNull()
		{
			this.TestCoreWithAutoVerify( default( MsgPack.Serialization.AddOnlyCollection<System.DateTime>[] ), this.GetSerializationContext() );
		}	
		
		[Test]
		public void TestObjectField()
		{
			this.TestCoreWithAutoVerify( new MessagePackObject( 1 ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestObjectFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new MessagePackObject( 1 ), 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestObjectFieldNull()
		{
			this.TestCoreWithAutoVerify( default( Object ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestObjectFieldArrayNull()
		{
			this.TestCoreWithAutoVerify( default( Object[] ), this.GetSerializationContext() );
		}	
		
		[Test]
		public void TestObjectArrayField()
		{
			this.TestCoreWithAutoVerify( new Object []{ new MessagePackObject( 1 ), new MessagePackObject( 2 ) }, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestObjectArrayFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new Object []{ new MessagePackObject( 1 ), new MessagePackObject( 2 ) }, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestObjectArrayFieldNull()
		{
			this.TestCoreWithAutoVerify( default( Object[] ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestObjectArrayFieldArrayNull()
		{
			this.TestCoreWithAutoVerify( default( Object[][] ), this.GetSerializationContext() );
		}	
		
#if !NETFX_CORE && !SILVERLIGHT
		[Test]
		public void TestArrayListField()
		{
			this.TestCoreWithAutoVerify( new ArrayList(){ new MessagePackObject( 1 ), new MessagePackObject( 2 ) }, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestArrayListFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new ArrayList(){ new MessagePackObject( 1 ), new MessagePackObject( 2 ) }, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestArrayListFieldNull()
		{
			this.TestCoreWithAutoVerify( default( ArrayList ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestArrayListFieldArrayNull()
		{
			this.TestCoreWithAutoVerify( default( ArrayList[] ), this.GetSerializationContext() );
		}	
		
#endif // !NETFX_CORE && !SILVERLIGHT
#if !NETFX_CORE && !SILVERLIGHT
		[Test]
		public void TestHashtableField()
		{
			this.TestCoreWithAutoVerify( new Hashtable(){ { new MessagePackObject( "1" ), new MessagePackObject( 1 ) }, { new MessagePackObject( "2" ), new MessagePackObject( 2 ) } }, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestHashtableFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new Hashtable(){ { new MessagePackObject( "1" ), new MessagePackObject( 1 ) }, { new MessagePackObject( "2" ), new MessagePackObject( 2 ) } }, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestHashtableFieldNull()
		{
			this.TestCoreWithAutoVerify( default( Hashtable ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestHashtableFieldArrayNull()
		{
			this.TestCoreWithAutoVerify( default( Hashtable[] ), this.GetSerializationContext() );
		}	
		
#endif // !NETFX_CORE && !SILVERLIGHT
		[Test]
		public void TestListObjectField()
		{
			this.TestCoreWithAutoVerify( new List<Object>(){ new MessagePackObject( 1 ), new MessagePackObject( 2 ) }, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestListObjectFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new List<Object>(){ new MessagePackObject( 1 ), new MessagePackObject( 2 ) }, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestListObjectFieldNull()
		{
			this.TestCoreWithAutoVerify( default( List<Object> ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestListObjectFieldArrayNull()
		{
			this.TestCoreWithAutoVerify( default( List<Object>[] ), this.GetSerializationContext() );
		}	
		
		[Test]
		public void TestDictionaryObjectObjectField()
		{
			this.TestCoreWithAutoVerify( new Dictionary<Object, Object>(){ { new MessagePackObject( "1" ), new MessagePackObject( 1 ) }, { new MessagePackObject( "2" ), new MessagePackObject( 2 ) } }, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestDictionaryObjectObjectFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new Dictionary<Object, Object>(){ { new MessagePackObject( "1" ), new MessagePackObject( 1 ) }, { new MessagePackObject( "2" ), new MessagePackObject( 2 ) } }, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestDictionaryObjectObjectFieldNull()
		{
			this.TestCoreWithAutoVerify( default( Dictionary<Object, Object> ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestDictionaryObjectObjectFieldArrayNull()
		{
			this.TestCoreWithAutoVerify( default( Dictionary<Object, Object>[] ), this.GetSerializationContext() );
		}	
		
		[Test]
		public void TestCollectionObjectField()
		{
			this.TestCoreWithAutoVerify( new Collection<Object>(){ new MessagePackObject( 1 ), new MessagePackObject( 2 ) }, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestCollectionObjectFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new Collection<Object>(){ new MessagePackObject( 1 ), new MessagePackObject( 2 ) }, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestCollectionObjectFieldNull()
		{
			this.TestCoreWithAutoVerify( default( Collection<Object> ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestCollectionObjectFieldArrayNull()
		{
			this.TestCoreWithAutoVerify( default( Collection<Object>[] ), this.GetSerializationContext() );
		}	
		
		[Test]
		public void TestStringKeyedCollection_ObjectField()
		{
			this.TestCoreWithAutoVerify( new StringKeyedCollection<Object>(){ new MessagePackObject( 1 ), new MessagePackObject( 2 ) }, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestStringKeyedCollection_ObjectFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new StringKeyedCollection<Object>(){ new MessagePackObject( 1 ), new MessagePackObject( 2 ) }, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestStringKeyedCollection_ObjectFieldNull()
		{
			this.TestCoreWithAutoVerify( default( MsgPack.Serialization.StringKeyedCollection<System.Object> ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestStringKeyedCollection_ObjectFieldArrayNull()
		{
			this.TestCoreWithAutoVerify( default( MsgPack.Serialization.StringKeyedCollection<System.Object>[] ), this.GetSerializationContext() );
		}	
		
#if !NETFX_35
		[Test]
		public void TestObservableCollectionObjectField()
		{
			this.TestCoreWithAutoVerify( new ObservableCollection<Object>(){ new MessagePackObject( 1 ), new MessagePackObject( 2 ) }, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestObservableCollectionObjectFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new ObservableCollection<Object>(){ new MessagePackObject( 1 ), new MessagePackObject( 2 ) }, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestObservableCollectionObjectFieldNull()
		{
			this.TestCoreWithAutoVerify( default( ObservableCollection<Object> ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestObservableCollectionObjectFieldArrayNull()
		{
			this.TestCoreWithAutoVerify( default( ObservableCollection<Object>[] ), this.GetSerializationContext() );
		}	
		
#endif // !NETFX_35
		[Test]
		public void TestHashSetObjectField()
		{
			this.TestCoreWithAutoVerify( new HashSet<Object>(){ new MessagePackObject( 1 ), new MessagePackObject( 2 ) }, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestHashSetObjectFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new HashSet<Object>(){ new MessagePackObject( 1 ), new MessagePackObject( 2 ) }, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestHashSetObjectFieldNull()
		{
			this.TestCoreWithAutoVerify( default( HashSet<Object> ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestHashSetObjectFieldArrayNull()
		{
			this.TestCoreWithAutoVerify( default( HashSet<Object>[] ), this.GetSerializationContext() );
		}	
		
		[Test]
		public void TestICollectionObjectField()
		{
			this.TestCoreWithAutoVerify( new SimpleCollection<Object>(){ new MessagePackObject( 1 ), new MessagePackObject( 2 ) }, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestICollectionObjectFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new SimpleCollection<Object>(){ new MessagePackObject( 1 ), new MessagePackObject( 2 ) }, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestICollectionObjectFieldNull()
		{
			this.TestCoreWithAutoVerify( default( ICollection<Object> ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestICollectionObjectFieldArrayNull()
		{
			this.TestCoreWithAutoVerify( default( ICollection<Object>[] ), this.GetSerializationContext() );
		}	
		
#if !NETFX_35
		[Test]
		public void TestISetObjectField()
		{
			this.TestCoreWithAutoVerify( new HashSet<Object>(){ new MessagePackObject( 1 ), new MessagePackObject( 2 ) }, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestISetObjectFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new HashSet<Object>(){ new MessagePackObject( 1 ), new MessagePackObject( 2 ) }, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestISetObjectFieldNull()
		{
			this.TestCoreWithAutoVerify( default( ISet<Object> ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestISetObjectFieldArrayNull()
		{
			this.TestCoreWithAutoVerify( default( ISet<Object>[] ), this.GetSerializationContext() );
		}	
		
#endif // !NETFX_35
		[Test]
		public void TestIListObjectField()
		{
			this.TestCoreWithAutoVerify( new List<Object>(){ new MessagePackObject( 1 ), new MessagePackObject( 2 ) }, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestIListObjectFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new List<Object>(){ new MessagePackObject( 1 ), new MessagePackObject( 2 ) }, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestIListObjectFieldNull()
		{
			this.TestCoreWithAutoVerify( default( IList<Object> ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestIListObjectFieldArrayNull()
		{
			this.TestCoreWithAutoVerify( default( IList<Object>[] ), this.GetSerializationContext() );
		}	
		
		[Test]
		public void TestIDictionaryObjectObjectField()
		{
			this.TestCoreWithAutoVerify( new Dictionary<Object, Object>(){ { new MessagePackObject( "1" ), new MessagePackObject( 1 ) }, { new MessagePackObject( "2" ), new MessagePackObject( 2 ) } }, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestIDictionaryObjectObjectFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new Dictionary<Object, Object>(){ { new MessagePackObject( "1" ), new MessagePackObject( 1 ) }, { new MessagePackObject( "2" ), new MessagePackObject( 2 ) } }, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestIDictionaryObjectObjectFieldNull()
		{
			this.TestCoreWithAutoVerify( default( IDictionary<Object, Object> ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestIDictionaryObjectObjectFieldArrayNull()
		{
			this.TestCoreWithAutoVerify( default( IDictionary<Object, Object>[] ), this.GetSerializationContext() );
		}	
		
		[Test]
		public void TestAddOnlyCollection_ObjectField()
		{
			this.TestCoreWithAutoVerify( new AddOnlyCollection<Object>(){ new MessagePackObject( 1 ), new MessagePackObject( 2 ) }, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestAddOnlyCollection_ObjectFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new AddOnlyCollection<Object>(){ new MessagePackObject( 1 ), new MessagePackObject( 2 ) }, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestAddOnlyCollection_ObjectFieldNull()
		{
			this.TestCoreWithAutoVerify( default( MsgPack.Serialization.AddOnlyCollection<System.Object> ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestAddOnlyCollection_ObjectFieldArrayNull()
		{
			this.TestCoreWithAutoVerify( default( MsgPack.Serialization.AddOnlyCollection<System.Object>[] ), this.GetSerializationContext() );
		}	
		
		[Test]
		public void TestMessagePackObject_Field()
		{
			this.TestCoreWithAutoVerify( new MessagePackObject( 1 ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestMessagePackObject_FieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new MessagePackObject( 1 ), 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestMessagePackObject_FieldNull()
		{
			this.TestCoreWithAutoVerify( default( MsgPack.MessagePackObject ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestMessagePackObject_FieldArrayNull()
		{
			this.TestCoreWithAutoVerify( default( MsgPack.MessagePackObject[] ), this.GetSerializationContext() );
		}	
		
		[Test]
		public void TestMessagePackObjectArray_Field()
		{
			this.TestCoreWithAutoVerify( new MessagePackObject []{ new MessagePackObject( 1 ), new MessagePackObject( 2 ) }, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestMessagePackObjectArray_FieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new MessagePackObject []{ new MessagePackObject( 1 ), new MessagePackObject( 2 ) }, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestMessagePackObjectArray_FieldNull()
		{
			this.TestCoreWithAutoVerify( default( MsgPack.MessagePackObject[] ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestMessagePackObjectArray_FieldArrayNull()
		{
			this.TestCoreWithAutoVerify( default( MsgPack.MessagePackObject[][] ), this.GetSerializationContext() );
		}	
		
		[Test]
		public void TestList_MessagePackObjectField()
		{
			this.TestCoreWithAutoVerify( new List<MessagePackObject>(){ new MessagePackObject( 1 ), new MessagePackObject( 2 ) }, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestList_MessagePackObjectFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new List<MessagePackObject>(){ new MessagePackObject( 1 ), new MessagePackObject( 2 ) }, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestList_MessagePackObjectFieldNull()
		{
			this.TestCoreWithAutoVerify( default( System.Collections.Generic.List<MsgPack.MessagePackObject> ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestList_MessagePackObjectFieldArrayNull()
		{
			this.TestCoreWithAutoVerify( default( System.Collections.Generic.List<MsgPack.MessagePackObject>[] ), this.GetSerializationContext() );
		}	
		
		[Test]
		public void TestDictionary_MessagePackObject_MessagePackObjectField()
		{
			this.TestCoreWithAutoVerify( new Dictionary<MessagePackObject, MessagePackObject>(){ { new MessagePackObject( "1" ), new MessagePackObject( 1 ) }, { new MessagePackObject( "2" ), new MessagePackObject( 2 ) } }, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestDictionary_MessagePackObject_MessagePackObjectFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new Dictionary<MessagePackObject, MessagePackObject>(){ { new MessagePackObject( "1" ), new MessagePackObject( 1 ) }, { new MessagePackObject( "2" ), new MessagePackObject( 2 ) } }, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestDictionary_MessagePackObject_MessagePackObjectFieldNull()
		{
			this.TestCoreWithAutoVerify( default( System.Collections.Generic.Dictionary<MsgPack.MessagePackObject, MsgPack.MessagePackObject> ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestDictionary_MessagePackObject_MessagePackObjectFieldArrayNull()
		{
			this.TestCoreWithAutoVerify( default( System.Collections.Generic.Dictionary<MsgPack.MessagePackObject, MsgPack.MessagePackObject>[] ), this.GetSerializationContext() );
		}	
		
		[Test]
		public void TestCollection_MessagePackObjectField()
		{
			this.TestCoreWithAutoVerify( new Collection<MessagePackObject>(){ new MessagePackObject( 1 ), new MessagePackObject( 2 ) }, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestCollection_MessagePackObjectFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new Collection<MessagePackObject>(){ new MessagePackObject( 1 ), new MessagePackObject( 2 ) }, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestCollection_MessagePackObjectFieldNull()
		{
			this.TestCoreWithAutoVerify( default( System.Collections.ObjectModel.Collection<MsgPack.MessagePackObject> ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestCollection_MessagePackObjectFieldArrayNull()
		{
			this.TestCoreWithAutoVerify( default( System.Collections.ObjectModel.Collection<MsgPack.MessagePackObject>[] ), this.GetSerializationContext() );
		}	
		
		[Test]
		public void TestStringKeyedCollection_MessagePackObjectField()
		{
			this.TestCoreWithAutoVerify( new StringKeyedCollection<MessagePackObject>(){ new MessagePackObject( 1 ), new MessagePackObject( 2 ) }, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestStringKeyedCollection_MessagePackObjectFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new StringKeyedCollection<MessagePackObject>(){ new MessagePackObject( 1 ), new MessagePackObject( 2 ) }, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestStringKeyedCollection_MessagePackObjectFieldNull()
		{
			this.TestCoreWithAutoVerify( default( MsgPack.Serialization.StringKeyedCollection<MsgPack.MessagePackObject> ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestStringKeyedCollection_MessagePackObjectFieldArrayNull()
		{
			this.TestCoreWithAutoVerify( default( MsgPack.Serialization.StringKeyedCollection<MsgPack.MessagePackObject>[] ), this.GetSerializationContext() );
		}	
		
#if !NETFX_35
		[Test]
		public void TestObservableCollection_MessagePackObjectField()
		{
			this.TestCoreWithAutoVerify( new ObservableCollection<MessagePackObject>(){ new MessagePackObject( 1 ), new MessagePackObject( 2 ) }, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestObservableCollection_MessagePackObjectFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new ObservableCollection<MessagePackObject>(){ new MessagePackObject( 1 ), new MessagePackObject( 2 ) }, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestObservableCollection_MessagePackObjectFieldNull()
		{
			this.TestCoreWithAutoVerify( default( System.Collections.ObjectModel.ObservableCollection<MsgPack.MessagePackObject> ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestObservableCollection_MessagePackObjectFieldArrayNull()
		{
			this.TestCoreWithAutoVerify( default( System.Collections.ObjectModel.ObservableCollection<MsgPack.MessagePackObject>[] ), this.GetSerializationContext() );
		}	
		
#endif // !NETFX_35
		[Test]
		public void TestHashSet_MessagePackObjectField()
		{
			this.TestCoreWithAutoVerify( new HashSet<MessagePackObject>(){ new MessagePackObject( 1 ), new MessagePackObject( 2 ) }, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestHashSet_MessagePackObjectFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new HashSet<MessagePackObject>(){ new MessagePackObject( 1 ), new MessagePackObject( 2 ) }, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestHashSet_MessagePackObjectFieldNull()
		{
			this.TestCoreWithAutoVerify( default( System.Collections.Generic.HashSet<MsgPack.MessagePackObject> ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestHashSet_MessagePackObjectFieldArrayNull()
		{
			this.TestCoreWithAutoVerify( default( System.Collections.Generic.HashSet<MsgPack.MessagePackObject>[] ), this.GetSerializationContext() );
		}	
		
		[Test]
		public void TestICollection_MessagePackObjectField()
		{
			this.TestCoreWithAutoVerify( new SimpleCollection<MessagePackObject>(){ new MessagePackObject( 1 ), new MessagePackObject( 2 ) }, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestICollection_MessagePackObjectFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new SimpleCollection<MessagePackObject>(){ new MessagePackObject( 1 ), new MessagePackObject( 2 ) }, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestICollection_MessagePackObjectFieldNull()
		{
			this.TestCoreWithAutoVerify( default( System.Collections.Generic.ICollection<MsgPack.MessagePackObject> ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestICollection_MessagePackObjectFieldArrayNull()
		{
			this.TestCoreWithAutoVerify( default( System.Collections.Generic.ICollection<MsgPack.MessagePackObject>[] ), this.GetSerializationContext() );
		}	
		
#if !NETFX_35
		[Test]
		public void TestISet_MessagePackObjectField()
		{
			this.TestCoreWithAutoVerify( new HashSet<MessagePackObject>(){ new MessagePackObject( 1 ), new MessagePackObject( 2 ) }, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestISet_MessagePackObjectFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new HashSet<MessagePackObject>(){ new MessagePackObject( 1 ), new MessagePackObject( 2 ) }, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestISet_MessagePackObjectFieldNull()
		{
			this.TestCoreWithAutoVerify( default( System.Collections.Generic.ISet<MsgPack.MessagePackObject> ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestISet_MessagePackObjectFieldArrayNull()
		{
			this.TestCoreWithAutoVerify( default( System.Collections.Generic.ISet<MsgPack.MessagePackObject>[] ), this.GetSerializationContext() );
		}	
		
#endif // !NETFX_35
		[Test]
		public void TestIList_MessagePackObjectField()
		{
			this.TestCoreWithAutoVerify( new List<MessagePackObject>(){ new MessagePackObject( 1 ), new MessagePackObject( 2 ) }, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestIList_MessagePackObjectFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new List<MessagePackObject>(){ new MessagePackObject( 1 ), new MessagePackObject( 2 ) }, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestIList_MessagePackObjectFieldNull()
		{
			this.TestCoreWithAutoVerify( default( System.Collections.Generic.IList<MsgPack.MessagePackObject> ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestIList_MessagePackObjectFieldArrayNull()
		{
			this.TestCoreWithAutoVerify( default( System.Collections.Generic.IList<MsgPack.MessagePackObject>[] ), this.GetSerializationContext() );
		}	
		
		[Test]
		public void TestIDictionary_MessagePackObject_MessagePackObjectField()
		{
			this.TestCoreWithAutoVerify( new Dictionary<MessagePackObject, MessagePackObject>(){ { new MessagePackObject( "1" ), new MessagePackObject( 1 ) }, { new MessagePackObject( "2" ), new MessagePackObject( 2 ) } }, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestIDictionary_MessagePackObject_MessagePackObjectFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new Dictionary<MessagePackObject, MessagePackObject>(){ { new MessagePackObject( "1" ), new MessagePackObject( 1 ) }, { new MessagePackObject( "2" ), new MessagePackObject( 2 ) } }, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestIDictionary_MessagePackObject_MessagePackObjectFieldNull()
		{
			this.TestCoreWithAutoVerify( default( System.Collections.Generic.IDictionary<MsgPack.MessagePackObject, MsgPack.MessagePackObject> ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestIDictionary_MessagePackObject_MessagePackObjectFieldArrayNull()
		{
			this.TestCoreWithAutoVerify( default( System.Collections.Generic.IDictionary<MsgPack.MessagePackObject, MsgPack.MessagePackObject>[] ), this.GetSerializationContext() );
		}	
		
		[Test]
		public void TestAddOnlyCollection_MessagePackObjectField()
		{
			this.TestCoreWithAutoVerify( new AddOnlyCollection<MessagePackObject>(){ new MessagePackObject( 1 ), new MessagePackObject( 2 ) }, this.GetSerializationContext() );
		}
		
		[Test]
		public void TestAddOnlyCollection_MessagePackObjectFieldArray()
		{
			this.TestCoreWithAutoVerify( Enumerable.Repeat( new AddOnlyCollection<MessagePackObject>(){ new MessagePackObject( 1 ), new MessagePackObject( 2 ) }, 2 ).ToArray(), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestAddOnlyCollection_MessagePackObjectFieldNull()
		{
			this.TestCoreWithAutoVerify( default( MsgPack.Serialization.AddOnlyCollection<MsgPack.MessagePackObject> ), this.GetSerializationContext() );
		}
		
		[Test]
		public void TestAddOnlyCollection_MessagePackObjectFieldArrayNull()
		{
			this.TestCoreWithAutoVerify( default( MsgPack.Serialization.AddOnlyCollection<MsgPack.MessagePackObject>[] ), this.GetSerializationContext() );
		}	
		
		[Test]
		public void TestComplexTypeGeneratedEnclosure_WithShortcut()
		{
			SerializerDebugging.AvoidsGenericSerializer = false;
			try 
			{
				var target = new ComplexTypeGeneratedEnclosure();
				target.Initialize();
				this.TestCoreWithVerifiable( target, this.GetSerializationContext() );
			}
			finally
			{
				SerializerDebugging.AvoidsGenericSerializer = false;
			}
		}

		[Test]
		public void TestComplexTypeGeneratedEnclosure_WithoutShortcut()
		{
			SerializerDebugging.AvoidsGenericSerializer = true;
			try 
			{
				var target = new ComplexTypeGeneratedEnclosure();
				target.Initialize();
				this.TestCoreWithVerifiable( target, this.GetSerializationContext() );
			}
			finally
			{
				SerializerDebugging.AvoidsGenericSerializer = false;
			}
		}
		
		[Test]
		public void TestComplexTypeGeneratedEnclosureArray_WithShortcut()
		{
			SerializerDebugging.AvoidsGenericSerializer = false;
			try 
			{
				this.TestCoreWithVerifiable( Enumerable.Repeat( 0, 2 ).Select( _ => new ComplexTypeGeneratedEnclosure().Initialize() ).ToArray(), this.GetSerializationContext() );
			}
			finally
			{
				SerializerDebugging.AvoidsGenericSerializer = false;
			}
		}
		
		[Test]
		public void TestComplexTypeGeneratedEnclosureArray_WithoutShortcut()
		{
			SerializerDebugging.AvoidsGenericSerializer = true;
			try 
			{
				this.TestCoreWithVerifiable( Enumerable.Repeat( 0, 2 ).Select( _ => new ComplexTypeGeneratedEnclosure().Initialize() ).ToArray(), this.GetSerializationContext() );
			}
			finally
			{
				SerializerDebugging.AvoidsGenericSerializer = false;
			}
		}
		
		[Test]
		public void TestComplexTypeGenerated_WithShortcut()
		{
			SerializerDebugging.AvoidsGenericSerializer = false;
			try 
			{
				var target = new ComplexTypeGenerated();
				target.Initialize();
				this.TestCoreWithVerifiable( target, this.GetSerializationContext() );
			}
			finally
			{
				SerializerDebugging.AvoidsGenericSerializer = false;
			}
		}

		[Test]
		public void TestComplexTypeGenerated_WithoutShortcut()
		{
			SerializerDebugging.AvoidsGenericSerializer = true;
			try 
			{
				var target = new ComplexTypeGenerated();
				target.Initialize();
				this.TestCoreWithVerifiable( target, this.GetSerializationContext() );
			}
			finally
			{
				SerializerDebugging.AvoidsGenericSerializer = false;
			}
		}
		
		[Test]
		public void TestComplexTypeGeneratedArray_WithShortcut()
		{
			SerializerDebugging.AvoidsGenericSerializer = false;
			try 
			{
				this.TestCoreWithVerifiable( Enumerable.Repeat( 0, 2 ).Select( _ => new ComplexTypeGenerated().Initialize() ).ToArray(), this.GetSerializationContext() );
			}
			finally
			{
				SerializerDebugging.AvoidsGenericSerializer = false;
			}
		}
		
		[Test]
		public void TestComplexTypeGeneratedArray_WithoutShortcut()
		{
			SerializerDebugging.AvoidsGenericSerializer = true;
			try 
			{
				this.TestCoreWithVerifiable( Enumerable.Repeat( 0, 2 ).Select( _ => new ComplexTypeGenerated().Initialize() ).ToArray(), this.GetSerializationContext() );
			}
			finally
			{
				SerializerDebugging.AvoidsGenericSerializer = false;
			}
		}

		private void TestCoreWithAutoVerify<T>( T value, SerializationContext context )
		{
			var target = this.CreateTarget<T>( context );
			using ( var buffer = new MemoryStream() )
			{
				target.Pack( buffer, value );
				buffer.Position = 0;
				T unpacked = target.Unpack( buffer );
				buffer.Position = 0;
				AutoMessagePackSerializerTest.Verify( value, unpacked );
			}
		}
		
		private void TestCoreWithVerifiable<T>( T value, SerializationContext context )
			where T : IVerifiable<T>
		{
			var target = this.CreateTarget<T>( context );
			using ( var buffer = new MemoryStream() )
			{
				target.Pack( buffer, value );
				buffer.Position = 0;
				T unpacked = target.Unpack( buffer );
				buffer.Position = 0;
				unpacked.Verify( value );
			}
		}	
		
		private void TestCoreWithVerifiable<T>( T[] value, SerializationContext context )
			where T : IVerifiable<T>
		{
			var target = this.CreateTarget<T[]>( context );
			using ( var buffer = new MemoryStream() )
			{
				target.Pack( buffer, value );
				buffer.Position = 0;
				T[] unpacked = target.Unpack( buffer );
				buffer.Position = 0;
				Assert.That( unpacked.Length, Is.EqualTo( value.Length ) );
				for( int i = 0; i < unpacked.Length; i ++ )
				{
					try
					{
						unpacked[ i ].Verify( value[ i ] );
					}
#if MSTEST
					catch( Microsoft.VisualStudio.TestPlatform.UnitTestFramework.AssertFailedException ae )
					{
						throw new Microsoft.VisualStudio.TestPlatform.UnitTestFramework.AssertFailedException( i.ToString(), ae );
					}
#else
					catch( AssertionException ae )
					{
						throw new AssertionException( i.ToString(), ae );
					}
#endif
				}
			}
		}	
		
		private static FILETIME ToFileTime( DateTime dateTime )
		{
			var fileTime = dateTime.ToFileTimeUtc();
			return new FILETIME(){ dwHighDateTime = unchecked( ( int )( fileTime >> 32 ) ), dwLowDateTime = unchecked( ( int )( fileTime & 0xffffffff ) ) };
		}
	}
}
