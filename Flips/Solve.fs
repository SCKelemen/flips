﻿module Flips.Solve


open Google.OrTools.LinearSolver
open Flips.Domain

type Solution = {
    DecisionResults : Map<Decision,float>
    ObjectiveResults : Map<Flips.Domain.Objective,float>
}

type SolverType = | CBC

type SolverSettings = {
    SolverType : SolverType
    SolveDuration : float
}


let private buildExpression (vars:Map<Decision,Variable>) (LinearExpression expr:LinearExpression) =
    let constantExpr =
        expr
        |> List.choose (fun x -> match x with | Empty -> Some 0.0 | Scalar s -> Some s | _ -> None)
        |> List.sum

    let decisionExpr =
        expr
        |> List.choose (fun x -> match x with | Variable (c, d) -> Some (c * vars.[d]) | _ -> None)
        |> fun x -> match x with | [] -> new LinearExpr() | l -> List.reduce (+) l
        
    constantExpr + decisionExpr

let private createVariable (solver:Solver) (DecisionName name:DecisionName) (decisionType:DecisionType) =
    match decisionType with
    | Boolean -> solver.MakeBoolVar(name)
    | Integer (lb, ub) -> solver.MakeIntVar(lb, ub, name)
    | Continuous (lb, ub) -> solver.MakeNumVar(lb, ub, name)

let private createVariableMap (solver:Solver) (decisions:Map<Decision, DecisionType>) =
    decisions
    |> Map.map (fun decision decisionType -> createVariable solver decision.Name decisionType)

let private setObjective (vars:Map<Decision, Variable>) (objective:Flips.Domain.Objective) (solver:Solver) =
    let expr = buildExpression vars objective.Expression

    match objective.Sense with
    | Minimize -> solver.Minimize(expr)
    | Maximize -> solver.Maximize(expr)

let private addConstraint (vars:Map<Decision, Variable>) (Constraint (lhs, comparison, rhs):Constraint) (solver:Solver) =
    let lhsExpr = buildExpression vars lhs
    let rhsExpr = buildExpression vars rhs
    let constraintExpr = lhsExpr - rhsExpr

    match comparison with
    | LessOrEqual -> 
        let c = new RangeConstraint(constraintExpr, System.Double.NegativeInfinity, 0.0)
        solver.Add(c)
    | GreaterOrEqual -> 
        let c = new RangeConstraint(constraintExpr, 0.0, System.Double.PositiveInfinity)
        solver.Add(c)
    | Equal -> 
        let c = new Equality(lhsExpr, rhsExpr, true)
        solver.Add(c)
    |> ignore

let private addConstraints (vars:Map<Decision, Variable>) (constraints:List<Constraint>) (solver:Solver) =
    for c in constraints do
        addConstraint vars c solver |> ignore