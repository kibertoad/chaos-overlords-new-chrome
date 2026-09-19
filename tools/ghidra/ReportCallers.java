// Prints direct call-site ownership without decompiling the callee.
// @category Rechaos

import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Function;
import ghidra.program.model.listing.Instruction;
import ghidra.program.model.symbol.Reference;
import ghidra.program.model.symbol.ReferenceIterator;

import java.util.TreeSet;

public class ReportCallers extends GhidraScript {
    @Override
    protected void run() throws Exception {
        String[] arguments = getScriptArgs();
        if (arguments.length == 0) {
            printerr("Supply one or more callee addresses.");
            return;
        }

        for (String argument : arguments) {
            Address callee = toAddr(argument);
            println("===== callers of " + callee + " =====");
            TreeSet<String> calls = new TreeSet<>();
            ReferenceIterator references = currentProgram.getReferenceManager().getReferencesTo(callee);
            while (references.hasNext()) {
                Reference reference = references.next();
                Instruction instruction = currentProgram.getListing()
                    .getInstructionAt(reference.getFromAddress());
                if (instruction == null || !instruction.getFlowType().isCall()) continue;
                Function owner = currentProgram.getFunctionManager()
                    .getFunctionContaining(instruction.getAddress());
                calls.add(instruction.getAddress()
                    + (owner == null ? "" : " in " + owner.getEntryPoint() + " " + owner.getName()));
            }
            if (calls.isEmpty()) println("No direct call sites found.");
            else for (String call : calls) println(call);
        }
    }
}
