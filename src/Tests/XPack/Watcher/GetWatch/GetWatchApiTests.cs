﻿using System;
using System.Linq;
using Elasticsearch.Net;
using FluentAssertions;
using Nest;
using Tests.Framework;
using Tests.Framework.Integration;
using Tests.Framework.ManagedElasticsearch.Clusters;
using Tests.Framework.MockData;

namespace Tests.XPack.Watcher.GetWatch
{
	public class GetWatchApiTests : ApiIntegrationTestBase<XPackCluster, IGetWatchResponse, IGetWatchRequest, GetWatchDescriptor, GetWatchRequest>
	{
		public GetWatchApiTests(XPackCluster cluster, EndpointUsage usage) : base(cluster, usage) { }

		protected override void IntegrationSetup(IElasticClient client, CallUniqueValues values) => PutWatch(client, values);

		public static void PutWatch(IElasticClient client, CallUniqueValues values)
		{
			foreach (var callUniqueValue in values)
			{
				var putWatchResponse = client.PutWatch(callUniqueValue.Value, p => p
					.Input(i => i
						.Chain(c => c
							.Input("simple", ci => ci
								.Simple(s => s
									.Add("str", "val1")
									.Add("num", 23)
									.Add("obj", new { str = "val2" })
								)
							)
							.Input("http", ci => ci
								.Http(h => h
									.Request(r => r
										.Host("localhost")
										.Port(8080)
										.Method(HttpInputMethod.Post)
										.Path("/path.html")
										.Proxy(pr => pr
											.Host("proxy")
											.Port(6000)
										)
										.Scheme(ConnectionScheme.Https)
										.Authentication(a => a
											.Basic(b => b
												.Username("Username123")
												.Password("Password123")
											)
										)
										.Body("{\"query\" : {\"range\": {\"@timestamp\" : {\"from\": \"{{ctx.trigger.triggered_time}}||-5m\",\"to\": \"{{ctx.trigger.triggered_time}}\"}}}}")
										.Headers(he => he
											.Add("header1", "value1")
										)
										.Params(pa => pa
											.Add("lat", "52.374031")
											.Add("lon", "4.88969")
											.Add("appid", "appid")
										)
										.ConnectionTimeout("3s")
										.ReadTimeout(TimeSpan.FromMilliseconds(500))
									)
									.ResponseContentType(ResponseContentType.Text)
								)
							)
							.Input("search", ci => ci
								.Search(s => s
									.Request(si => si
										.Indices<Project>()
										.Body<Project>(b => b
											.Size(0)
											.Aggregations(a => a
												.Nested("nested_tags", n => n
													.Path(np => np.Tags)
													.Aggregations(aa => aa
														.Terms("top_project_tags", ta => ta
															.Field(f => f.Tags.First().Name)
														)
													)
												)
											)
										)
									)
								)
							)
						)
					)
					.Trigger(t => t
						.Schedule(s => s
							.Cron("0 5 9 * * ?")
						)
					)
					.Transform(tr => tr
						.Chain(ct => ct
							.Transform(ctt => ctt
								.Search(st => st
									.Request(str => str
										.Indices(typeof(Project))
										.SearchType(SearchType.DfsQueryThenFetch)
										.IndicesOptions(io => io
											.ExpandWildcards(ExpandWildcards.Open)
											.IgnoreUnavailable()
										)
										.Body<Project>(b => b
											.Query(q => q
												.Match(m => m
													.Field("state")
													.Query(StateOfBeing.Stable.ToString().ToLowerInvariant())
												)
											)
										)
									)
									.Timeout("10s")
								)
							)
							.Transform(ctt => ctt
								.Script(st => st
									.Source("return [ 'time' : ctx.trigger.scheduled_time ]")
								)
							)
						)
					)
					.Actions(a => a
						.Email("reminder_email", e => e
							.To("me@example.com")
							.Subject("Something's strange in the neighbourhood")
							.Body(b => b
								.Text("Dear {{ctx.payload.name}}, by the time you read these lines, I'll be gone")
							)
							.Attachments(ea => ea
								.HttpAttachment("http_attachment", ha => ha
									.Inline()
									.ContentType(RequestData.MimeType)
									.Request(r => r
										.Url("http://localhost:8080/http_attachment")
									)
								)
								.DataAttachment("data_attachment", da => da
									.Format(DataAttachmentFormat.Json)
								)
							)
						)
						.Index("reminder_index", i => i
							.Index("put-watch-test-index")
							.DocType("reminder")
							.ExecutionTimeField("execution_time")
						)
						.PagerDuty("reminder_pagerduty", pd => pd
							.Account("my_pagerduty_account")
							.Description("pager duty description")
							.AttachPayload()
							.EventType(PagerDutyEventType.Trigger)
							.IncidentKey("incident_key")
							.Context(c => c
								.Context(PagerDutyContextType.Image, cd => cd
									.Src("http://example.com/image")
								)
								.Context(PagerDutyContextType.Link, cd => cd
									.Href("http://example.com/link")
								)
							)
						)
						.Slack("reminder_slack", sl => sl
							.Account("monitoring")
							.Message(sm => sm
								.From("nest integration test")
								.To("#nest")
								.Text("slack message")
								.Attachments(sa => sa
									.Attachment(saa => saa
										.Title("Attachment 1")
										.AuthorName("Russ Cam")
									)
								)
							)
						)
						.HipChat("reminder_hipchat", hc => hc
							.Account("notify-monitoring")
							.Message(hm => hm
								.Body("hipchat message")
								.Color(HipChatMessageColor.Purple)
								.Room("nest")
								.Notify()
							)
						)
						.Webhook("webhook", w => w
							.Scheme(ConnectionScheme.Https)
							.Host("localhost")
							.Port(9200)
							.Method(HttpInputMethod.Post)
							.Path("/_bulk")
							.Authentication(au => au
								.Basic(b => b
									.Username("username")
									.Password("password")
								)
							)
							.Body("{{ctx.payload._value}}")
						)
					)
				);

				if (!putWatchResponse.IsValid)
					throw new Exception($"Problem setting up integration test: {putWatchResponse.DebugInformation}");
			}
		}

		protected override LazyResponses ClientUsage() => Calls(
			fluent: (client, f) => client.GetWatch(CallIsolatedValue, f),
			fluentAsync: (client, f) => client.GetWatchAsync(CallIsolatedValue, f),
			request: (client, r) => client.GetWatch(r),
			requestAsync: (client, r) => client.GetWatchAsync(r)
		);

		protected override bool ExpectIsValid => true;
		protected override int ExpectStatusCode => 200;
		protected override HttpMethod HttpMethod => HttpMethod.GET;

		protected override string UrlPath => $"/_xpack/watcher/watch/{CallIsolatedValue}";

		protected override bool SupportsDeserialization => true;

		protected override GetWatchDescriptor NewDescriptor() => new GetWatchDescriptor(CallIsolatedValue);

		protected override object ExpectJson => null;

		protected override Func<GetWatchDescriptor, IGetWatchRequest> Fluent => f => f;

		protected override GetWatchRequest Initializer =>
			new GetWatchRequest(CallIsolatedValue);

		protected override void ExpectResponse(IGetWatchResponse response)
		{
			response.Found.Should().BeTrue();
			response.Id.Should().Be(CallIsolatedValue);

			var watchStatus = response.Status;
			watchStatus.Should().NotBeNull();
			watchStatus.Version.Should().Be(1);
			watchStatus.State.Should().NotBeNull();
			watchStatus.State.Active.Should().BeTrue();
			watchStatus.State.Timestamp.Should().BeBefore(DateTimeOffset.UtcNow);

			watchStatus.Actions.Should().NotBeNull().And.ContainKey("reminder_email");
			var watchStatusAction = watchStatus.Actions["reminder_email"];

			watchStatusAction.Acknowledgement.State.Should().Be(AcknowledgementState.AwaitsSuccessfulExecution);
			watchStatusAction.Acknowledgement.Timestamp.Should().BeBefore(DateTimeOffset.UtcNow);

			var watch = response.Watch;
			watch.Should().NotBeNull();

			var trigger = watch.Trigger;
			trigger.Should().NotBeNull();
			trigger.Schedule.Should().NotBeNull();
			trigger.Schedule.Cron.Should().NotBeNull();
			trigger.Schedule.Cron.ToString().Should().Be("0 5 9 * * ?");

			watch.Input.Should().NotBeNull();
			var chainInput = watch.Input.Chain;
			chainInput.Should().NotBeNull();
			chainInput.Inputs.Should().NotBeNull().And.HaveCount(3);

			var simpleInput = ((IInputContainer)chainInput.Inputs["simple"]).Simple;
			simpleInput.Should().NotBeNull();
			simpleInput.Payload.Should().NotBeNull();

			var httpInput = ((IInputContainer)chainInput.Inputs["http"]).Http;
			httpInput.Should().NotBeNull();
			httpInput.Request.Should().NotBeNull();

			var searchInput = ((IInputContainer)chainInput.Inputs["search"]).Search;
			searchInput.Should().NotBeNull();
			searchInput.Request.Should().NotBeNull();

			watch.Transform.Should().NotBeNull();
			watch.Transform.Chain.Should().NotBeNull();
			var chainTransforms = watch.Transform.Chain.Transforms;
			chainTransforms.Should().NotBeNull().And.HaveCount(2);
			var firstTransform = chainTransforms.First();
			firstTransform.Should().NotBeNull();
			((ITransformContainer)firstTransform).Search.Should().NotBeNull();

			var lastTransform = chainTransforms.Last();
			lastTransform.Should().NotBeNull();
			((ITransformContainer)lastTransform).Script.Should().NotBeNull();

			watch.Condition.Should().NotBeNull();
			watch.Condition.Always.Should().NotBeNull();

			watch.Actions.Should().NotBeNull().And.ContainKey("reminder_email");
			watch.Actions.Should().NotBeNull().And.ContainKey("reminder_index");
			watch.Actions.Should().NotBeNull().And.ContainKey("reminder_pagerduty");
			watch.Actions.Should().NotBeNull().And.ContainKey("reminder_slack");
			watch.Actions.Should().NotBeNull().And.ContainKey("reminder_hipchat");
			watch.Actions.Should().NotBeNull().And.ContainKey("webhook");

			var webhook = (IWebhookAction)watch.Actions["webhook"];
			webhook.Authentication.Should().NotBeNull();
		}
	}

	public class GetNonExistentWatchApiTests : ApiIntegrationTestBase<XPackCluster, IGetWatchResponse, IGetWatchRequest, GetWatchDescriptor, GetWatchRequest>
	{
		protected override void IntegrationSetup(IElasticClient client, CallUniqueValues values) => GetWatchApiTests.PutWatch(client, values);

		public GetNonExistentWatchApiTests(XPackCluster cluster, EndpointUsage usage) : base(cluster, usage) { }

		protected override LazyResponses ClientUsage() => Calls(
			fluent: (client, f) => client.GetWatch(CallIsolatedValue + "x", f),
			fluentAsync: (client, f) => client.GetWatchAsync(CallIsolatedValue + "x", f),
			request: (client, r) => client.GetWatch(r),
			requestAsync: (client, r) => client.GetWatchAsync(r)
		);

		protected override bool ExpectIsValid => false;
		protected override int ExpectStatusCode => 404;
		protected override HttpMethod HttpMethod => HttpMethod.GET;

		protected override string UrlPath => $"/_xpack/watcher/watch/{CallIsolatedValue + "x"}";

		protected override bool SupportsDeserialization => true;

		protected override GetWatchDescriptor NewDescriptor() => new GetWatchDescriptor(CallIsolatedValue + "x");

		protected override object ExpectJson => null;

		protected override Func<GetWatchDescriptor, IGetWatchRequest> Fluent => f => f;

		protected override GetWatchRequest Initializer => new GetWatchRequest(CallIsolatedValue + "x");

		protected override void ExpectResponse(IGetWatchResponse response)
		{
			response.Found.Should().BeFalse();
			response.Id.Should().Be(CallIsolatedValue + "x");
			response.Status.Should().BeNull();
			response.Watch.Should().BeNull();
		}
	}
}
