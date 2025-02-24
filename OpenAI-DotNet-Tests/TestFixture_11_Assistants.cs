﻿using NUnit.Framework;
using OpenAI.Assistants;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace OpenAI.Tests
{
    internal class TestFixture_11_Assistants : AbstractTestFixture
    {
        private AssistantResponse testAssistant;

        [Test]
        public async Task Test_01_CreateAssistant()
        {
            Assert.IsNotNull(OpenAIClient.AssistantsEndpoint);
            const string testFilePath = "assistant_test_1.txt";
            await File.WriteAllTextAsync(testFilePath, "Knowledge is power!");
            Assert.IsTrue(File.Exists(testFilePath));
            var file = await OpenAIClient.FilesEndpoint.UploadFileAsync(testFilePath, "assistants");
            File.Delete(testFilePath);
            Assert.IsFalse(File.Exists(testFilePath));
            var request = new CreateAssistantRequest("gpt-3.5-turbo-1106",
                name: "test-assistant",
                description: "Used for unit testing.",
                instructions: "You are test assistant",
                metadata: new Dictionary<string, string>
                {
                    ["int"] = "1",
                    ["test"] = Guid.NewGuid().ToString()
                },
                tools: new[]
                {
                    Tool.Retrieval
                },
                files: new[] { file.Id });
            var assistant = await OpenAIClient.AssistantsEndpoint.CreateAssistantAsync(request);
            Assert.IsNotNull(assistant);
            Assert.AreEqual("test-assistant", assistant.Name);
            Assert.AreEqual("Used for unit testing.", assistant.Description);
            Assert.AreEqual("You are test assistant", assistant.Instructions);
            Assert.AreEqual("gpt-3.5-turbo-1106", assistant.Model);
            Assert.IsNotEmpty(assistant.Metadata);
            testAssistant = assistant;
            Console.WriteLine($"{assistant} -> {assistant.Metadata["test"]}");
        }

        [Test]
        public async Task Test_02_ListAssistants()
        {
            Assert.IsNotNull(OpenAIClient.AssistantsEndpoint);
            var assistantsList = await OpenAIClient.AssistantsEndpoint.ListAssistantsAsync();
            Assert.IsNotNull(assistantsList);
            Assert.IsNotEmpty(assistantsList.Items);

            foreach (var assistant in assistantsList.Items)
            {
                var retrieved = OpenAIClient.AssistantsEndpoint.RetrieveAssistantAsync(assistant);
                Assert.IsNotNull(retrieved);
                Console.WriteLine($"{assistant} -> {assistant.CreatedAt}");
            }
        }

        [Test]
        public async Task Test_03_ModifyAssistants()
        {
            Assert.IsNotNull(testAssistant);
            Assert.IsNotNull(OpenAIClient.AssistantsEndpoint);
            var request = new CreateAssistantRequest(
                model: "gpt-4-1106-preview",
                name: "Test modified",
                description: "Modified description",
                instructions: "You are modified test assistant");
            var assistant = await testAssistant.ModifyAsync(request);
            Assert.IsNotNull(assistant);
            Assert.AreEqual("Test modified", assistant.Name);
            Assert.AreEqual("Modified description", assistant.Description);
            Assert.AreEqual("You are modified test assistant", assistant.Instructions);
            Assert.AreEqual("gpt-4-1106-preview", assistant.Model);
            Assert.IsTrue(assistant.Metadata.ContainsKey("test"));
            Console.WriteLine($"{assistant.Id} -> modified");
        }

        [Test]
        public async Task Test_04_01_UploadAssistantFile()
        {
            Assert.IsNotNull(testAssistant);
            Assert.IsNotNull(OpenAIClient.AssistantsEndpoint);
            const string testFilePath = "assistant_test_2.txt";
            await File.WriteAllTextAsync(testFilePath, "Knowledge is power!");
            Assert.IsTrue(File.Exists(testFilePath));
            var file = testAssistant.UploadFileAsync(testFilePath);
            Assert.IsNotNull(file);
        }

        [Test]
        public async Task Test_04_02_ListAssistantFiles()
        {
            Assert.IsNotNull(testAssistant);
            Assert.IsNotNull(OpenAIClient.AssistantsEndpoint);
            var filesList = await testAssistant.ListFilesAsync();
            Assert.IsNotNull(filesList);
            Assert.IsNotEmpty(filesList.Items);

            foreach (var file in filesList.Items)
            {
                Assert.IsNotNull(file);
                var retrieved = await testAssistant.RetrieveFileAsync(file);
                Assert.IsNotNull(retrieved);
                Assert.IsTrue(retrieved.Id == file.Id);
                Console.WriteLine($"{retrieved.AssistantId}'s file -> {retrieved.Id}");
                // TODO 400 Bad Request error when attempting to download assistant files. Likely OpenAI bug.
                //var downloadPath = await retrieved.DownloadFileAsync(Directory.GetCurrentDirectory(), true);
                //Console.WriteLine($"downloaded {retrieved} -> {downloadPath}");
                //Assert.IsTrue(File.Exists(downloadPath));
                //File.Delete(downloadPath);
                //Assert.IsFalse(File.Exists(downloadPath));
            }
        }

        [Test]
        public async Task Test_04_03_DeleteAssistantFiles()
        {
            Assert.IsNotNull(testAssistant);
            Assert.IsNotNull(OpenAIClient.AssistantsEndpoint);
            var filesList = await testAssistant.ListFilesAsync();
            Assert.IsNotNull(filesList);
            Assert.IsNotEmpty(filesList.Items);

            foreach (var file in filesList.Items)
            {
                Assert.IsNotNull(file);
                var isDeleted = await testAssistant.DeleteFileAsync(file);
                Assert.IsTrue(isDeleted);
                Console.WriteLine($"Deleted {file.Id}");
            }

            filesList = await testAssistant.ListFilesAsync();
            Assert.IsNotNull(filesList);
            Assert.IsEmpty(filesList.Items);
        }

        [Test]
        public async Task Test_05_DeleteAssistant()
        {
            Assert.IsNotNull(testAssistant);
            Assert.IsNotNull(OpenAIClient.AssistantsEndpoint);
            var result = await testAssistant.DeleteAsync();
            Assert.IsTrue(result);
            Console.WriteLine($"{testAssistant.Id} -> deleted");
        }
    }
}