// Writes a function inventory of the current program to a tab-separated file outside the repository.
// One row per function: entry, last byte of the body, body size, caller count, callee entries,
// imported API names, and the initialized-data addresses the function reads and writes.
// No instruction text is written. Input for tools/spec-coverage.mjs.
// @category Rechaos

import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.address.AddressSetView;
import ghidra.program.model.listing.Function;
import ghidra.program.model.listing.Instruction;
import ghidra.program.model.mem.MemoryBlock;
import ghidra.program.model.symbol.Reference;

import java.io.File;
import java.io.PrintWriter;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.List;
import java.util.Set;
import java.util.TreeSet;

public class ReportFunctionInventory extends GhidraScript {
    @Override
    protected void run() throws Exception {
        String[] arguments = getScriptArgs();
        if (arguments.length != 1) {
            printerr("Supply the output file path (outside the repository).");
            return;
        }
        File output = new File(arguments[0]).getAbsoluteFile();
        String sourceDir = getSourceFile() == null ? null
            : new File(getSourceFile().getAbsolutePath()).getParentFile().getParentFile().getParentFile().getAbsolutePath();
        if (sourceDir != null && output.getPath().toLowerCase().startsWith(sourceDir.toLowerCase() + File.separator)) {
            printerr("Refusing to write the inventory inside the repository: " + output);
            return;
        }

        int count = 0;
        try (PrintWriter writer = new PrintWriter(output, StandardCharsets.UTF_8)) {
            writer.println("entry\tend\tbytes\tcallers\tcallees\timports\treads\twrites");
            for (Function function : currentProgram.getFunctionManager().getFunctions(true)) {
                monitor.checkCancelled();
                AddressSetView body = function.getBody();
                List<String> callees = new ArrayList<>();
                Set<String> imports = new TreeSet<>();
                for (Function callee : function.getCalledFunctions(monitor)) {
                    Function target = callee.isThunk() ? callee.getThunkedFunction(true) : callee;
                    if (target != null && target.isExternal()) {
                        imports.add(target.getName());
                    }
                    else {
                        callees.add(callee.getEntryPoint().toString());
                    }
                }
                Set<String> reads = new TreeSet<>();
                Set<String> writes = new TreeSet<>();
                for (Instruction instruction : currentProgram.getListing().getInstructions(body, true)) {
                    for (Reference reference : instruction.getReferencesFrom()) {
                        if (!reference.getReferenceType().isData()) {
                            continue;
                        }
                        Address to = reference.getToAddress();
                        MemoryBlock block = currentProgram.getMemory().getBlock(to);
                        if (block == null || block.isExecute()) {
                            continue;
                        }
                        String name = block.getName() + ":" + to;
                        if (reference.getReferenceType().isWrite()) {
                            writes.add(name);
                        }
                        if (reference.getReferenceType().isRead() || !reference.getReferenceType().isWrite()) {
                            reads.add(name);
                        }
                    }
                }
                writer.println(function.getEntryPoint() + "\t" + body.getMaxAddress() + "\t"
                    + body.getNumAddresses() + "\t" + function.getCallingFunctions(monitor).size() + "\t"
                    + String.join(",", callees) + "\t" + String.join(",", imports) + "\t"
                    + String.join(",", reads) + "\t" + String.join(",", writes));
                count++;
            }
        }
        println("Wrote " + count + " functions to " + output);
    }
}
