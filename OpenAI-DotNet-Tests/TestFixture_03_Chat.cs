﻿using NUnit.Framework;
using OpenAI.Chat;
using OpenAI.Models;
using OpenAI.Tests.Weather;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace OpenAI.Tests
{
    internal class TestFixture_03_Chat : AbstractTestFixture
    {
        [Test]
        public async Task Test_01_GetChatCompletion()
        {
            Assert.IsNotNull(OpenAIClient.ChatEndpoint);
            var messages = new List<Message>
            {
                new Message(Role.System, "You are a helpful assistant."),
                new Message(Role.User, "Who won the world series in 2020?"),
                new Message(Role.Assistant, "The Los Angeles Dodgers won the World Series in 2020."),
                new Message(Role.User, "Where was it played?"),
            };
            var chatRequest = new ChatRequest(messages, Model.GPT4);
            var response = await OpenAIClient.ChatEndpoint.GetCompletionAsync(chatRequest);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Choices);
            Assert.IsNotEmpty(response.Choices);

            foreach (var choice in response.Choices)
            {
                Console.WriteLine($"[{choice.Index}] {choice.Message.Role}: {choice} | Finish Reason: {choice.FinishReason}");
            }

            response.GetUsage();
        }

        [Test]
        public async Task Test_02_GetChatStreamingCompletion()
        {
            Assert.IsNotNull(OpenAIClient.ChatEndpoint);
            var messages = new List<Message>
            {
                new Message(Role.System, "You are a helpful assistant."),
                new Message(Role.User, "Who won the world series in 2020?"),
                new Message(Role.Assistant, "The Los Angeles Dodgers won the World Series in 2020."),
                new Message(Role.User, "Where was it played?"),
            };
            var chatRequest = new ChatRequest(messages);
            var cumulativeDelta = string.Empty;
            var response = await OpenAIClient.ChatEndpoint.StreamCompletionAsync(chatRequest, partialResponse =>
            {
                Assert.IsNotNull(partialResponse);
                Assert.NotNull(partialResponse.Choices);
                Assert.NotZero(partialResponse.Choices.Count);

                foreach (var choice in partialResponse.Choices.Where(choice => choice.Delta?.Content != null))
                {
                    cumulativeDelta += choice.Delta.Content;
                }
            });
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Choices);
            var choice = response.FirstChoice;
            Assert.IsNotNull(choice);
            Assert.IsNotNull(choice.Message);
            Assert.IsFalse(string.IsNullOrEmpty(choice.Message.Content));
            Console.WriteLine($"[{choice.Index}] {choice.Message.Role}: {choice} | Finish Reason: {choice.FinishReason}");
            Assert.IsTrue(choice.Message.Role == Role.Assistant);
            Assert.IsTrue(choice.Message.Content!.Equals(cumulativeDelta));
            Console.WriteLine(response.ToString());
            response.GetUsage();
        }

        [Test]
        public async Task Test_03_GetChatStreamingCompletionEnumerableAsync()
        {
            Assert.IsNotNull(OpenAIClient.ChatEndpoint);
            var messages = new List<Message>
            {
                new Message(Role.System, "You are a helpful assistant."),
                new Message(Role.User, "Who won the world series in 2020?"),
                new Message(Role.Assistant, "The Los Angeles Dodgers won the World Series in 2020."),
                new Message(Role.User, "Where was it played?"),
            };
            var cumulativeDelta = string.Empty;
            var chatRequest = new ChatRequest(messages);
            await foreach (var partialResponse in OpenAIClient.ChatEndpoint.StreamCompletionEnumerableAsync(chatRequest))
            {
                Assert.IsNotNull(partialResponse);
                Assert.NotNull(partialResponse.Choices);
                Assert.NotZero(partialResponse.Choices.Count);

                foreach (var choice in partialResponse.Choices.Where(choice => choice.Delta?.Content != null))
                {
                    cumulativeDelta += choice.Delta.Content;
                }
            }

            Console.WriteLine(cumulativeDelta);
        }

        //[Test]
        [Obsolete]
        public async Task Test_04_GetChatFunctionCompletion()
        {
            Assert.IsNotNull(OpenAIClient.ChatEndpoint);
            var messages = new List<Message>
            {
                new Message(Role.System, "You are a helpful weather assistant."),
                new Message(Role.User, "What's the weather like today?"),
            };

            foreach (var message in messages)
            {
                Console.WriteLine($"{message.Role}: {message.Content}");
            }

            var functions = new List<Function>
            {
                new Function(
                    nameof(WeatherService.GetCurrentWeather),
                    "Get the current weather in a given location",
                     new JsonObject
                     {
                         ["type"] = "object",
                         ["properties"] = new JsonObject
                         {
                             ["location"] = new JsonObject
                             {
                                 ["type"] = "string",
                                 ["description"] = "The city and state, e.g. San Francisco, CA"
                             },
                             ["unit"] = new JsonObject
                             {
                                 ["type"] = "string",
                                 ["enum"] = new JsonArray {"celsius", "fahrenheit"}
                             }
                         },
                         ["required"] = new JsonArray { "location", "unit" }
                     })
            };

            var chatRequest = new ChatRequest(messages, functions: functions, functionCall: "auto");
            var response = await OpenAIClient.ChatEndpoint.GetCompletionAsync(chatRequest);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Choices);
            Assert.IsTrue(response.Choices.Count == 1);
            messages.Add(response.FirstChoice.Message);

            Console.WriteLine($"{response.FirstChoice.Message.Role}: {response.FirstChoice} | Finish Reason: {response.FirstChoice.FinishReason}");

            var locationMessage = new Message(Role.User, "I'm in Glasgow, Scotland");
            messages.Add(locationMessage);
            Console.WriteLine($"{locationMessage.Role}: {locationMessage.Content}");
            chatRequest = new ChatRequest(messages, functions: functions, functionCall: "auto");
            response = await OpenAIClient.ChatEndpoint.GetCompletionAsync(chatRequest);

            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Choices);
            Assert.IsTrue(response.Choices.Count == 1);
            messages.Add(response.FirstChoice.Message);

            if (!string.IsNullOrEmpty(response.FirstChoice.Message.Content))
            {
                Console.WriteLine($"{response.FirstChoice.Message.Role}: {response.FirstChoice} | Finish Reason: {response.FirstChoice.FinishReason}");

                var unitMessage = new Message(Role.User, "celsius");
                messages.Add(unitMessage);
                Console.WriteLine($"{unitMessage.Role}: {unitMessage.Content}");
                chatRequest = new ChatRequest(messages, functions: functions, functionCall: "auto");
                response = await OpenAIClient.ChatEndpoint.GetCompletionAsync(chatRequest);
                Assert.IsNotNull(response);
                Assert.IsNotNull(response.Choices);
                Assert.IsTrue(response.Choices.Count == 1);
            }

            Assert.IsTrue(response.FirstChoice.FinishReason == "function_call");
            Assert.IsTrue(response.FirstChoice.Message.Function.Name == nameof(WeatherService.GetCurrentWeather));
            Console.WriteLine($"{response.FirstChoice.Message.Role}: {response.FirstChoice.Message.Function.Name} | Finish Reason: {response.FirstChoice.FinishReason}");
            Console.WriteLine($"{response.FirstChoice.Message.Function.Arguments}");
            var functionArgs = JsonSerializer.Deserialize<WeatherArgs>(response.FirstChoice.Message.Function.Arguments.ToString());
            var functionResult = WeatherService.GetCurrentWeather(functionArgs);
            Assert.IsNotNull(functionResult);
            messages.Add(new Message(Role.Function, functionResult, nameof(WeatherService.GetCurrentWeather)));
            Console.WriteLine($"{Role.Function}: {functionResult}");
            chatRequest = new ChatRequest(messages, functions: functions, functionCall: "auto");
            response = await OpenAIClient.ChatEndpoint.GetCompletionAsync(chatRequest);
            Console.WriteLine(response);
        }

        //[Test]
        [Obsolete]
        public async Task Test_05_GetChatFunctionCompletion_Streaming()
        {
            var messages = new List<Message>
            {
                new Message(Role.System, "You are a helpful weather assistant."),
                new Message(Role.User, "What's the weather like today?"),
            };

            foreach (var message in messages)
            {
                Console.WriteLine($"{message.Role}: {message.Content}");
            }

            var functions = new List<Function>
            {
                new Function(
                    nameof(WeatherService.GetCurrentWeather),
                    "Get the current weather in a given location",
                    new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject
                        {
                            ["location"] = new JsonObject
                            {
                                ["type"] = "string",
                                ["description"] = "The city and state, e.g. San Francisco, CA"
                            },
                            ["unit"] = new JsonObject
                            {
                                ["type"] = "string",
                                ["enum"] = new JsonArray {"celsius", "fahrenheit"}
                            }
                        },
                        ["required"] = new JsonArray { "location", "unit" }
                    })
            };

            var chatRequest = new ChatRequest(messages, functions: functions, functionCall: "auto");
            var response = await OpenAIClient.ChatEndpoint.StreamCompletionAsync(chatRequest, partialResponse =>
            {
                Assert.IsNotNull(partialResponse);
                Assert.NotNull(partialResponse.Choices);
                Assert.NotZero(partialResponse.Choices.Count);
            });
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Choices);
            Assert.IsTrue(response.Choices.Count == 1);
            messages.Add(response.FirstChoice.Message);

            var locationMessage = new Message(Role.User, "I'm in Glasgow, Scotland");
            messages.Add(locationMessage);
            Console.WriteLine($"{locationMessage.Role}: {locationMessage.Content}");
            chatRequest = new ChatRequest(messages, functions: functions, functionCall: "auto");
            response = await OpenAIClient.ChatEndpoint.StreamCompletionAsync(chatRequest, partialResponse =>
            {
                Assert.IsNotNull(partialResponse);
                Assert.NotNull(partialResponse.Choices);
                Assert.NotZero(partialResponse.Choices.Count);
            });
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Choices);
            Assert.IsTrue(response.Choices.Count == 1);
            messages.Add(response.FirstChoice.Message);

            if (!string.IsNullOrEmpty(response.FirstChoice.Message.Content))
            {
                Console.WriteLine($"{response.FirstChoice.Message.Role}: {response.FirstChoice} | Finish Reason: {response.FirstChoice.FinishReason}");

                var unitMessage = new Message(Role.User, "celsius");
                messages.Add(unitMessage);
                Console.WriteLine($"{unitMessage.Role}: {unitMessage.Content}");
                chatRequest = new ChatRequest(messages, functions: functions, functionCall: "auto");
                response = await OpenAIClient.ChatEndpoint.StreamCompletionAsync(chatRequest, partialResponse =>
                {
                    Assert.IsNotNull(partialResponse);
                    Assert.NotNull(partialResponse.Choices);
                    Assert.NotZero(partialResponse.Choices.Count);
                });
                Assert.IsNotNull(response);
                Assert.IsNotNull(response.Choices);
                Assert.IsTrue(response.Choices.Count == 1);
            }

            Assert.IsTrue(response.FirstChoice.FinishReason == "function_call");
            Assert.IsTrue(response.FirstChoice.Message.Function.Name == nameof(WeatherService.GetCurrentWeather));
            Console.WriteLine($"{response.FirstChoice.Message.Role}: {response.FirstChoice.Message.Function.Name} | Finish Reason: {response.FirstChoice.FinishReason}");
            Console.WriteLine($"{response.FirstChoice.Message.Function.Arguments}");

            var functionArgs = JsonSerializer.Deserialize<WeatherArgs>(response.FirstChoice.Message.Function.Arguments.ToString());
            var functionResult = WeatherService.GetCurrentWeather(functionArgs);
            Assert.IsNotNull(functionResult);
            messages.Add(new Message(Role.Function, functionResult, nameof(WeatherService.GetCurrentWeather)));
            Console.WriteLine($"{Role.Function}: {functionResult}");
        }

        //[Test]
        [Obsolete]
        public async Task Test_06_GetChatFunctionForceCompletion()
        {
            Assert.IsNotNull(OpenAIClient.ChatEndpoint);
            var messages = new List<Message>
            {
                new Message(Role.System, "You are a helpful weather assistant."),
                new Message(Role.User, "What's the weather like today?"),
            };

            foreach (var message in messages)
            {
                Console.WriteLine($"{message.Role}: {message.Content}");
            }

            var functions = new List<Function>
            {
                new Function(
                    nameof(WeatherService.GetCurrentWeather),
                    "Get the current weather in a given location",
                     new JsonObject
                     {
                         ["type"] = "object",
                         ["properties"] = new JsonObject
                         {
                             ["location"] = new JsonObject
                             {
                                 ["type"] = "string",
                                 ["description"] = "The city and state, e.g. San Francisco, CA"
                             },
                             ["unit"] = new JsonObject
                             {
                                 ["type"] = "string",
                                 ["enum"] = new JsonArray {"celsius", "fahrenheit"}
                             }
                         },
                         ["required"] = new JsonArray { "location", "unit" }
                     })
            };

            var chatRequest = new ChatRequest(messages, functions: functions);
            var response = await OpenAIClient.ChatEndpoint.GetCompletionAsync(chatRequest);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Choices);
            Assert.IsTrue(response.Choices.Count == 1);
            messages.Add(response.FirstChoice.Message);

            Console.WriteLine($"{response.FirstChoice.Message.Role}: {response.FirstChoice} | Finish Reason: {response.FirstChoice.FinishReason}");

            var locationMessage = new Message(Role.User, "I'm in Glasgow, Scotland");
            messages.Add(locationMessage);
            Console.WriteLine($"{locationMessage.Role}: {locationMessage.Content}");
            chatRequest = new ChatRequest(
                messages,
                functions: functions,
                functionCall: nameof(WeatherService.GetCurrentWeather),
                model: "gpt-3.5-turbo-0613");
            response = await OpenAIClient.ChatEndpoint.GetCompletionAsync(chatRequest);

            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Choices);
            Assert.IsTrue(response.Choices.Count == 1);
            messages.Add(response.FirstChoice.Message);

            Assert.IsTrue(response.FirstChoice.FinishReason == "stop");
            Assert.IsTrue(response.FirstChoice.Message.Function.Name == nameof(WeatherService.GetCurrentWeather));
            Console.WriteLine($"{response.FirstChoice.Message.Role}: {response.FirstChoice.Message.Function.Name} | Finish Reason: {response.FirstChoice.FinishReason}");
            Console.WriteLine($"{response.FirstChoice.Message.Function.Arguments}");
            var functionArgs = JsonSerializer.Deserialize<WeatherArgs>(response.FirstChoice.Message.Function.Arguments.ToString());
            var functionResult = WeatherService.GetCurrentWeather(functionArgs);
            Assert.IsNotNull(functionResult);
            messages.Add(new Message(Role.Function, functionResult, nameof(WeatherService.GetCurrentWeather)));
            Console.WriteLine($"{Role.Function}: {functionResult}");
        }

        [Test]
        public async Task Test_07_GetChatToolCompletion()
        {
            Assert.IsNotNull(OpenAIClient.ChatEndpoint);

            var messages = new List<Message>
            {
                new Message(Role.System, "You are a helpful weather assistant."),
                new Message(Role.User, "What's the weather like today?"),
            };

            foreach (var message in messages)
            {
                Console.WriteLine($"{message.Role}: {message.Content}");
            }

            var tools = new List<Tool>
            {
                new Function(
                    nameof(WeatherService.GetCurrentWeather),
                    "Get the current weather in a given location",
                     new JsonObject
                     {
                         ["type"] = "object",
                         ["properties"] = new JsonObject
                         {
                             ["location"] = new JsonObject
                             {
                                 ["type"] = "string",
                                 ["description"] = "The city and state, e.g. San Francisco, CA"
                             },
                             ["unit"] = new JsonObject
                             {
                                 ["type"] = "string",
                                 ["enum"] = new JsonArray {"celsius", "fahrenheit"}
                             }
                         },
                         ["required"] = new JsonArray { "location", "unit" }
                     })
            };
            var chatRequest = new ChatRequest(messages, tools: tools, toolChoice: "auto");
            var response = await OpenAIClient.ChatEndpoint.GetCompletionAsync(chatRequest);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Choices);
            Assert.IsTrue(response.Choices.Count == 1);
            messages.Add(response.FirstChoice.Message);

            Console.WriteLine($"{response.FirstChoice.Message.Role}: {response.FirstChoice} | Finish Reason: {response.FirstChoice.FinishReason}");

            var locationMessage = new Message(Role.User, "I'm in Glasgow, Scotland");
            messages.Add(locationMessage);
            Console.WriteLine($"{locationMessage.Role}: {locationMessage.Content}");
            chatRequest = new ChatRequest(messages, tools: tools, toolChoice: "auto");
            response = await OpenAIClient.ChatEndpoint.GetCompletionAsync(chatRequest);

            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Choices);
            Assert.IsTrue(response.Choices.Count == 1);
            messages.Add(response.FirstChoice.Message);

            if (!string.IsNullOrEmpty(response.FirstChoice))
            {
                Console.WriteLine($"{response.FirstChoice.Message.Role}: {response.FirstChoice} | Finish Reason: {response.FirstChoice.FinishReason}");

                var unitMessage = new Message(Role.User, "celsius");
                messages.Add(unitMessage);
                Console.WriteLine($"{unitMessage.Role}: {unitMessage.Content}");
                chatRequest = new ChatRequest(messages, tools: tools, toolChoice: "auto");
                response = await OpenAIClient.ChatEndpoint.GetCompletionAsync(chatRequest);
                Assert.IsNotNull(response);
                Assert.IsNotNull(response.Choices);
                Assert.IsTrue(response.Choices.Count == 1);
            }

            Assert.IsTrue(response.FirstChoice.FinishReason == "tool_calls");
            var usedTool = response.FirstChoice.Message.ToolCalls[0];
            Assert.IsNotNull(usedTool);
            Assert.IsTrue(usedTool.Function.Name == nameof(WeatherService.GetCurrentWeather));
            Console.WriteLine($"{response.FirstChoice.Message.Role}: {usedTool.Function.Name} | Finish Reason: {response.FirstChoice.FinishReason}");
            Console.WriteLine($"{usedTool.Function.Arguments}");
            var functionArgs = JsonSerializer.Deserialize<WeatherArgs>(usedTool.Function.Arguments.ToString());
            var functionResult = WeatherService.GetCurrentWeather(functionArgs);
            Assert.IsNotNull(functionResult);
            messages.Add(new Message(usedTool, functionResult));
            Console.WriteLine($"{Role.Tool}: {functionResult}");
            chatRequest = new ChatRequest(messages, tools: tools, toolChoice: "auto");
            response = await OpenAIClient.ChatEndpoint.GetCompletionAsync(chatRequest);
            Console.WriteLine(response);
        }

        [Test]
        public async Task Test_08_GetChatToolCompletion_Streaming()
        {
            var messages = new List<Message>
            {
                new Message(Role.System, "You are a helpful weather assistant."),
                new Message(Role.User, "What's the weather like today?"),
            };

            foreach (var message in messages)
            {
                Console.WriteLine($"{message.Role}: {message.Content}");
            }

            var tools = new List<Tool>
            {
                new Function(
                    nameof(WeatherService.GetCurrentWeather),
                    "Get the current weather in a given location",
                    new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject
                        {
                            ["location"] = new JsonObject
                            {
                                ["type"] = "string",
                                ["description"] = "The city and state, e.g. San Francisco, CA"
                            },
                            ["unit"] = new JsonObject
                            {
                                ["type"] = "string",
                                ["enum"] = new JsonArray {"celsius", "fahrenheit"}
                            }
                        },
                        ["required"] = new JsonArray { "location", "unit" }
                    })
            };
            var chatRequest = new ChatRequest(messages, tools: tools, toolChoice: "auto");
            var response = await OpenAIClient.ChatEndpoint.StreamCompletionAsync(chatRequest, partialResponse =>
            {
                Assert.IsNotNull(partialResponse);
                Assert.NotNull(partialResponse.Choices);
                Assert.NotZero(partialResponse.Choices.Count);
            });
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Choices);
            Assert.IsTrue(response.Choices.Count == 1);
            messages.Add(response.FirstChoice.Message);

            var locationMessage = new Message(Role.User, "I'm in Glasgow, Scotland");
            messages.Add(locationMessage);
            Console.WriteLine($"{locationMessage.Role}: {locationMessage.Content}");
            chatRequest = new ChatRequest(messages, tools: tools, toolChoice: "auto");
            response = await OpenAIClient.ChatEndpoint.StreamCompletionAsync(chatRequest, partialResponse =>
            {
                Assert.IsNotNull(partialResponse);
                Assert.NotNull(partialResponse.Choices);
                Assert.NotZero(partialResponse.Choices.Count);
            });
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Choices);
            Assert.IsTrue(response.Choices.Count == 1);
            messages.Add(response.FirstChoice.Message);

            if (!string.IsNullOrEmpty(response.FirstChoice.Message.Content))
            {
                Console.WriteLine($"{response.FirstChoice.Message.Role}: {response.FirstChoice} | Finish Reason: {response.FirstChoice.FinishReason}");

                var unitMessage = new Message(Role.User, "celsius");
                messages.Add(unitMessage);
                Console.WriteLine($"{unitMessage.Role}: {unitMessage.Content}");
                chatRequest = new ChatRequest(messages, tools: tools, toolChoice: "auto");
                response = await OpenAIClient.ChatEndpoint.StreamCompletionAsync(chatRequest, partialResponse =>
                {
                    Assert.IsNotNull(partialResponse);
                    Assert.NotNull(partialResponse.Choices);
                    Assert.NotZero(partialResponse.Choices.Count);
                });
                Assert.IsNotNull(response);
                Assert.IsNotNull(response.Choices);
                Assert.IsTrue(response.Choices.Count == 1);
            }

            Assert.IsTrue(response.FirstChoice.FinishReason == "tool_calls");
            var usedTool = response.FirstChoice.Message.ToolCalls[0];
            Assert.IsNotNull(usedTool);
            Assert.IsTrue(usedTool.Function.Name == nameof(WeatherService.GetCurrentWeather));
            Console.WriteLine($"{response.FirstChoice.Message.Role}: {usedTool.Function.Name} | Finish Reason: {response.FirstChoice.FinishReason}");
            Console.WriteLine($"{usedTool.Function.Arguments}");

            var functionArgs = JsonSerializer.Deserialize<WeatherArgs>(usedTool.Function.Arguments.ToString());
            var functionResult = WeatherService.GetCurrentWeather(functionArgs);
            Assert.IsNotNull(functionResult);
            messages.Add(new Message(usedTool, functionResult));
            Console.WriteLine($"{Role.Tool}: {functionResult}");

            chatRequest = new ChatRequest(messages, tools: tools, toolChoice: "auto");
            response = await OpenAIClient.ChatEndpoint.StreamCompletionAsync(chatRequest, partialResponse =>
            {
                Assert.IsNotNull(partialResponse);
                Assert.NotNull(partialResponse.Choices);
                Assert.NotZero(partialResponse.Choices.Count);
            });
            Assert.IsNotNull(response);
        }

        [Test]
        public async Task Test_09_GetChatToolForceCompletion()
        {
            Assert.IsNotNull(OpenAIClient.ChatEndpoint);
            var messages = new List<Message>
            {
                new Message(Role.System, "You are a helpful weather assistant."),
                new Message(Role.User, "What's the weather like today?"),
            };

            foreach (var message in messages)
            {
                Console.WriteLine($"{message.Role}: {message.Content}");
            }

            var tools = new List<Tool>
            {
                new Function(
                    nameof(WeatherService.GetCurrentWeather),
                    "Get the current weather in a given location",
                     new JsonObject
                     {
                         ["type"] = "object",
                         ["properties"] = new JsonObject
                         {
                             ["location"] = new JsonObject
                             {
                                 ["type"] = "string",
                                 ["description"] = "The city and state, e.g. San Francisco, CA"
                             },
                             ["unit"] = new JsonObject
                             {
                                 ["type"] = "string",
                                 ["enum"] = new JsonArray {"celsius", "fahrenheit"}
                             }
                         },
                         ["required"] = new JsonArray { "location", "unit" }
                     })
            };
            var chatRequest = new ChatRequest(messages, tools: tools);
            var response = await OpenAIClient.ChatEndpoint.GetCompletionAsync(chatRequest);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Choices);
            Assert.IsTrue(response.Choices.Count == 1);
            messages.Add(response.FirstChoice.Message);

            Console.WriteLine($"{response.FirstChoice.Message.Role}: {response.FirstChoice} | Finish Reason: {response.FirstChoice.FinishReason}");

            var locationMessage = new Message(Role.User, "I'm in Glasgow, Scotland");
            messages.Add(locationMessage);
            Console.WriteLine($"{locationMessage.Role}: {locationMessage.Content}");
            chatRequest = new ChatRequest(
                messages,
                tools: tools,
                toolChoice: nameof(WeatherService.GetCurrentWeather));
            response = await OpenAIClient.ChatEndpoint.GetCompletionAsync(chatRequest);

            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Choices);
            Assert.IsTrue(response.Choices.Count == 1);
            messages.Add(response.FirstChoice.Message);

            Assert.IsTrue(response.FirstChoice.FinishReason == "stop");
            var usedTool = response.FirstChoice.Message.ToolCalls[0];
            Assert.IsNotNull(usedTool);
            Assert.IsTrue(usedTool.Function.Name == nameof(WeatherService.GetCurrentWeather));
            Console.WriteLine($"{response.FirstChoice.Message.Role}: {usedTool.Function.Name} | Finish Reason: {response.FirstChoice.FinishReason}");
            Console.WriteLine($"{usedTool.Function.Arguments}");
            var functionArgs = JsonSerializer.Deserialize<WeatherArgs>(usedTool.Function.Arguments.ToString());
            var functionResult = WeatherService.GetCurrentWeather(functionArgs);
            Assert.IsNotNull(functionResult);
            messages.Add(new Message(usedTool, functionResult));
            Console.WriteLine($"{Role.Tool}: {functionResult}");
        }

        [Test]
        public async Task Test_10_GetChatVision()
        {
            Assert.IsNotNull(OpenAIClient.ChatEndpoint);
            var messages = new List<Message>
            {
                new Message(Role.System, "You are a helpful assistant."),
                new Message(Role.User, new List<Content>
                {
                    new Content(ContentType.Text, "What's in this image?"),
                    new ImageUrl("https://upload.wikimedia.org/wikipedia/commons/thumb/d/dd/Gfp-wisconsin-madison-the-nature-boardwalk.jpg/2560px-Gfp-wisconsin-madison-the-nature-boardwalk.jpg", ImageDetail.Low)
                })
            };
            var chatRequest = new ChatRequest(messages, model: "gpt-4-vision-preview");
            var response = await OpenAIClient.ChatEndpoint.GetCompletionAsync(chatRequest);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Choices);
            Console.WriteLine($"{response.FirstChoice.Message.Role}: {response.FirstChoice} | Finish Reason: {response.FirstChoice.FinishDetails}");
            response.GetUsage();
        }

        [Test]
        public async Task Test_11_GetChatVisionStreaming()
        {
            Assert.IsNotNull(OpenAIClient.ChatEndpoint);
            var messages = new List<Message>
            {
                new Message(Role.System, "You are a helpful assistant."),
                new Message(Role.User, new List<Content>
                {
                    new Content(ContentType.Text, "What's in this image?"),
                    new ImageUrl("https://upload.wikimedia.org/wikipedia/commons/thumb/d/dd/Gfp-wisconsin-madison-the-nature-boardwalk.jpg/2560px-Gfp-wisconsin-madison-the-nature-boardwalk.jpg", ImageDetail.Low)
                })
            };
            var chatRequest = new ChatRequest(messages, model: "gpt-4-vision-preview");
            var response = await OpenAIClient.ChatEndpoint.StreamCompletionAsync(chatRequest, partialResponse =>
            {
                Assert.IsNotNull(partialResponse);
                Assert.NotNull(partialResponse.Choices);
                Assert.NotZero(partialResponse.Choices.Count);
            });
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Choices);
            Console.WriteLine($"{response.FirstChoice.Message.Role}: {response.FirstChoice} | Finish Reason: {response.FirstChoice.FinishDetails}");
            response.GetUsage();
        }

        [Test]
        public async Task Test_12_JsonMode()
        {
            Assert.IsNotNull(OpenAIClient.ChatEndpoint);
            var messages = new List<Message>
            {
                new Message(Role.System, "You are a helpful assistant designed to output JSON."),
                new Message(Role.User, "Who won the world series in 2020?"),
            };
            var chatRequest = new ChatRequest(messages, "gpt-4-1106-preview", responseFormat: ChatResponseFormat.Json);
            var response = await OpenAIClient.ChatEndpoint.GetCompletionAsync(chatRequest);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Choices);
            Assert.IsNotEmpty(response.Choices);

            foreach (var choice in response.Choices)
            {
                Console.WriteLine($"[{choice.Index}] {choice.Message.Role}: {choice} | Finish Reason: {choice.FinishReason}");
            }

            response.GetUsage();
        }
    }
}