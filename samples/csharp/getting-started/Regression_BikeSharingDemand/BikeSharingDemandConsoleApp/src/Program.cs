﻿using System;
using BikeSharingDemand.Helpers;
using Microsoft.ML;
using Microsoft.ML.Core.Data;
using Microsoft.ML.Runtime;
using Microsoft.ML.Runtime.Api;

using BikeSharingDemand.DataStructures;

namespace BikeSharingDemand
{
    internal static class Program
    {
        private static string ModelsLocation = @"../../../../models";

        private static string DatasetsLocation = @"../../../../data";
        private static string TrainingDataLocation = $"{DatasetsLocation}/hour_train.csv";
        private static string TestDataLocation = $"{DatasetsLocation}/hour_test.csv";
        
        static void Main(string[] args)
        {
            // Create MLContext to be shared across the model creation workflow objects 
            // Set a random seed for repeatable/deterministic results across multiple trainings.
            var mlContext = new MLContext(seed: 0);

            // 1. Common data loading
            DataLoader dataLoader = new DataLoader(mlContext);
            var trainingDataView = dataLoader.GetDataView(TrainingDataLocation);
            var testDataView = dataLoader.GetDataView(TestDataLocation);

            // 2. Common data pre-process with pipeline data transformations
            var dataPreprocessor = new DataPreprocessor(mlContext);
            var dataPreprocessPipeline = dataPreprocessor.DataPreprocessPipeline;

            // (Optional) Peek data in training DataView after applying the PreprocessPipeline's transformations  
            ConsoleHelper.PeekDataViewInConsole(mlContext, trainingDataView, dataPreprocessPipeline, 10);
            ConsoleHelper.PeekFeaturesColumnDataInConsole(mlContext, "Features", trainingDataView, dataPreprocessPipeline, 10);

            // Definition of regression trainers/algorithms to use
            var regressionLearners = new (string name, IEstimator<ITransformer> value)[]
            {
                ("FastTree", mlContext.Regression.Trainers.FastTree()),
                ("Poisson", mlContext.Regression.Trainers.PoissonRegression()),
                ("SDCA", mlContext.Regression.Trainers.StochasticDualCoordinateAscent())
                //Other possible learners that could be included
                //...FastForestRegressor...
                //...FastTreeTweedieRegressor...
                //...GeneralizedAdditiveModelRegressor...
                //...OnlineGradientDescent... (Might need to normalize the features first)
            };

            // 3. Phase for Training, Evaluation and model file persistence
            // Per each regression trainer: Train, Evaluate, and Save a different model
            foreach (var learner in regressionLearners)
            {
                Console.WriteLine("================== Training model ==================");
                var modelBuilder = new ModelBuilder(mlContext, dataPreprocessPipeline, learner.value);
                var trainedModel = modelBuilder.Train(trainingDataView);

                Console.WriteLine("========= Predict a single data point ===============");
                var prediction = modelBuilder.PredictSingle(DemandObservationSample.SingleDemandSampleData);
                ConsoleHelper.PrintPrediction(prediction);

                Console.WriteLine("===== Evaluating Model's accuracy with Test data =====");
                var metrics = modelBuilder.Evaluate(testDataView);
                ConsoleHelper.PrintRegressionMetrics(learner.name, metrics);

                //Save the model file that can be used by any application
                modelBuilder.SaveAsFile($"{ModelsLocation}/{learner.name}Model.zip");
            }

            // 4. Test/Predict with the created models
            // The following test predictions could be implemented/deployed in a different application (production apps)
            // that's why it is seggregated from the previous loop
            // For each trained model, test 10 predictions           
            foreach (var learner in regressionLearners)
            {
                //Load current model
                var trainedModel = ModelTester.LoadModelFromZipFile(mlContext, $"{ModelsLocation}/{learner.name}Model.zip");
                Console.WriteLine($"================== Visualize/test 10 predictions for model {learner.name}Model.zip ==================");
                //Visualize 10 tests comparing prediction with actual/observed values from the test dataset
                ModelTester.VisualizeSomePredictions(learner.name, TestDataLocation, trainedModel, 10);
            }
            Console.ReadLine();
        }
    }
}
