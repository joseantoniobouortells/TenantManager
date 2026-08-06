Feature: Excepciones de Gastos por Categoría
  Como administrador del sistema (arrendador)
  Quiero poder definir porcentajes específicos para diferentes categorías de gastos dentro de un mismo contrato
  Para poder repartir consumos (luz, agua, internet) de manera no simétrica entre los inquilinos según sus condiciones pactadas

  Background:
    Given que existe una categoría de gasto "Luz" con Id 1 y es imputable
    And que existe una categoría de gasto "Agua" con Id 2 y es imputable
    And que el inquilino "Pepe Masanet" tiene un contrato activo

  Scenario: Cálculo de gastos sin excepciones usa el porcentaje por defecto
    Given que el contrato de Pepe tiene un VariableExpensePercentage de "33.33"
    And el contrato no tiene excepciones configuradas
    And existe una factura de "Luz" de 100€
    And existe una factura de "Agua" de 60€
    When el sistema calcula los gastos pendientes para el mes actual
    Then el gasto calculado para la factura de Luz debe ser 33.33€
    And el gasto calculado para la factura de Agua debe ser 20€
    And el total de gastos a cobrar debe ser 53.33€

  Scenario: Cálculo de gastos con excepciones respeta el override por categoría
    Given que el contrato de Pepe tiene un VariableExpensePercentage de "33.33"
    And el contrato tiene una excepción para la categoría "Luz" con un porcentaje de "30.00"
    And existe una factura de "Luz" de 100€
    And existe una factura de "Agua" de 60€
    When el sistema calcula los gastos pendientes para el mes actual
    Then el gasto calculado para la factura de Luz debe ser 30€
    And el gasto calculado para la factura de Agua debe ser 20€
    And el total de gastos a cobrar debe ser 50.00€

  Scenario: Añadir una excepción en la vista de edición de contratos
    Given que estoy editando el contrato de Pepe
    When selecciono la categoría "Internet" del selector de excepciones
    And introduzco el porcentaje "50"
    And pulso en "Añadir Excepción"
    Then la tabla de excepciones muestra "Internet: 50%"
    And al pulsar "Guardar Contrato", la excepción se inserta en la base de datos vinculada a dicho contrato
