// Re-export HandRuntime types so tests resolve them without explicit namespace.
// Avoids ambiguity: tests use HybridTherapist.Application.Hand facades for
// ConversationBuilder/CheckpointLibrary/WireConvention, but need the raw
// HandTurn/HandCheckpoint/HandExchange records from HandRuntime.
global using HandTurn = HandRuntime.HandTurn;
global using HandExchange = HandRuntime.HandExchange;
global using HandCheckpoint = HandRuntime.HandCheckpoint;
